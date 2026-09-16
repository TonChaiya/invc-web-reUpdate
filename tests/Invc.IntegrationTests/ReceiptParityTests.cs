using Dapper;
using Invc.Core.Receipts;
using Invc.Infrastructure.Data;
using Invc.Infrastructure.Receipts;
using Xunit.Abstractions;

namespace Invc.IntegrationTests;

/// <summary>
/// Phase 5 parity checks C10a–C10j (docs/report-parity-matrix.md) — read-only against live INV.
/// Raw side: direct SQL over OTH_IVO / OTH_IVOC / RCV_TYPE using the proven RECEIVE_NO key and DATE_RECEIVE fiscal year.
/// </summary>
public class ReceiptParityTests(ProductionReadOnlyFixture db, ITestOutputHelper output) : IClassFixture<ProductionReadOnlyFixture>
{
    private const string FyExpr = "CASE WHEN MONTH(h.DATE_RECEIVE) >= 10 THEN YEAR(h.DATE_RECEIVE) + 544 ELSE YEAR(h.DATE_RECEIVE) + 543 END";

    private async Task<(int fy, ReceiptReport report, NonPoReceiptRepository repo)> LoadAsync(string? typeCode = null)
    {
        var connections = db.RequireOrSkip();
        var repo = new NonPoReceiptRepository(connections);
        var years = await repo.GetFiscalYearsWithDataAsync();
        var fy = ReceiptDefaultPeriod.Choose(years, DateTime.Today, out var note);
        output.WriteLine($"{DateTime.Now:O} fiscal year {fy} (years with data: {string.Join(",", years)}; note: {note ?? "-"})");
        var headers = await repo.GetHeadersAsync(fy, null);
        return (fy, ReceiptReport.Build(fy, headers, typeCode, null), repo);
    }

    [SkippableFact]
    public async Task C10a_C10b_header_and_line_counts_match_raw_for_the_period_and_overall()
    {
        var (fy, report, repo) = await LoadAsync();
        var connections = db.RequireOrSkip();
        await using var c = await connections.OpenAsync();

        var rawHeaders = await c.ExecuteScalarAsync<int>(ReadOnlySql.Ensure($"SELECT COUNT(*) FROM dbo.OTH_IVO h WHERE {FyExpr} = @Fy"), new { Fy = fy });
        var rawLines = await c.ExecuteScalarAsync<int>(ReadOnlySql.Ensure(
            $"SELECT COUNT(*) FROM dbo.OTH_IVOC c WHERE EXISTS (SELECT 1 FROM dbo.OTH_IVO h WHERE h.RECEIVE_NO = c.RECEIVE_NO AND {FyExpr} = @Fy)"), new { Fy = fy });
        var allHeaders = await c.ExecuteScalarAsync<int>(ReadOnlySql.Ensure("SELECT COUNT(*) FROM dbo.OTH_IVO"));
        var allLines = await c.ExecuteScalarAsync<int>(ReadOnlySql.Ensure("SELECT COUNT(*) FROM dbo.OTH_IVOC"));

        // Overall: sum over every fiscal year with data must equal the whole table.
        var years = await repo.GetFiscalYearsWithDataAsync();
        var sumHeaders = 0; var sumLines = 0;
        foreach (var y in years)
        {
            var r = ReceiptReport.Build(y, await repo.GetHeadersAsync(y, null), null, null);
            sumHeaders += r.HeaderCount; sumLines += r.LineCount;
        }

        output.WriteLine($"C10a FY{fy}: raw headers {rawHeaders}, new {report.HeaderCount}; all years raw {allHeaders}, new {sumHeaders}");
        output.WriteLine($"C10b FY{fy}: raw lines {rawLines}, new {report.LineCount}; all years raw {allLines}, new {sumLines}");
        Assert.Equal(rawHeaders, report.HeaderCount);
        Assert.Equal(rawLines, report.LineCount);
        Assert.Equal(allHeaders, sumHeaders);
        Assert.Equal(allLines, sumLines);
    }

    [SkippableFact]
    public async Task C10c_detail_lines_equal_raw_line_set_for_representative_headers()
    {
        var (fy, report, repo) = await LoadAsync();
        Skip.If(report.HeaderCount == 0, $"no receipts in FY{fy}");
        var connections = db.RequireOrSkip();
        await using var c = await connections.OpenAsync();

        var picks = new List<NonPoReceiptSummary>();
        picks.AddRange(report.Headers.OrderBy(h => h.LineCount).Take(1));                                   // fewest lines
        picks.AddRange(report.Headers.OrderByDescending(h => h.LineCount).Take(1));                         // most lines
        picks.AddRange(report.Headers.Where(h => h.TypeCode is "10" or "11").Take(1));                     // CUP requisition
        picks.AddRange(report.Headers.Where(h => h.TypeCode is not ("10" or "11")).Take(1));               // non-CUP
        picks = picks.DistinctBy(h => h.ReceiveNo).ToList();

        foreach (var h in picks)
        {
            var raw = (await c.QueryAsync<(string WorkingCode, decimal Qty, decimal Pack, decimal? Unit, string? Lot, DateTime? Exp, string? Loc)>(ReadOnlySql.Ensure(
                "SELECT WORKING_CODE, QTY_ORDER, PACK_RATIO, UNIT_VALUE, LOTNO, EXPIRED_DATE, LOCATION FROM dbo.OTH_IVOC WHERE RECEIVE_NO = @R ORDER BY RECORD_NUMBER"),
                new { R = h.ReceiveNo })).ToList();
            var detail = await repo.GetDetailAsync(h.ReceiveNo);
            Assert.NotNull(detail);
            output.WriteLine($"C10c {h.ReceiveNo} type {h.TypeCode}: raw lines {raw.Count}, new {detail!.Lines.Count}, list LineCount {h.LineCount}");
            Assert.Equal(raw.Count, detail.Lines.Count);
            Assert.Equal(h.LineCount, detail.Lines.Count);
            foreach (var (r, n) in raw.Zip(detail.Lines))
            {
                Assert.Equal(r.WorkingCode, n.WorkingCode);
                Assert.Equal(r.Qty, n.QtyOrder);
                Assert.Equal(r.Pack, n.PackRatio);
                Assert.Equal(r.Unit, n.UnitValue);
                Assert.Equal(r.Lot, n.LotNo);
                Assert.Equal(r.Exp, n.ExpiredDate);
                Assert.Equal(r.Loc, n.Location);
            }
        }
    }

    [SkippableFact]
    public async Task C10d_C10e_type_distribution_matches_raw_including_unknown_codes()
    {
        var (fy, report, _) = await LoadAsync();
        var connections = db.RequireOrSkip();
        await using var c = await connections.OpenAsync();

        var raw = (await c.QueryAsync<(string Code, int Headers, int Lines, decimal Value)>(ReadOnlySql.Ensure($"""
            SELECT RTRIM(h.RCV_TYPE), COUNT(*),
                   (SELECT COUNT(*) FROM dbo.OTH_IVOC c JOIN dbo.OTH_IVO h2 ON h2.RECEIVE_NO = c.RECEIVE_NO
                     WHERE RTRIM(h2.RCV_TYPE) = RTRIM(h.RCV_TYPE) AND (CASE WHEN MONTH(h2.DATE_RECEIVE) >= 10 THEN YEAR(h2.DATE_RECEIVE) + 544 ELSE YEAR(h2.DATE_RECEIVE) + 543 END) = @Fy),
                   ISNULL(SUM(h.TOTAL_VALUE), 0)
            FROM dbo.OTH_IVO h WHERE {FyExpr} = @Fy GROUP BY RTRIM(h.RCV_TYPE) ORDER BY RTRIM(h.RCV_TYPE)
            """), new { Fy = fy })).ToList();
        var orphanTypes = await c.ExecuteScalarAsync<int>(ReadOnlySql.Ensure(
            "SELECT COUNT(*) FROM dbo.OTH_IVO h WHERE NOT EXISTS (SELECT 1 FROM dbo.RCV_TYPE t WHERE t.RCV_TYPE_CODE = RTRIM(h.RCV_TYPE))"));

        Assert.Equal(raw.Count, report.TypeBreakdown.Count);
        foreach (var (r, n) in raw.Zip(report.TypeBreakdown))
        {
            output.WriteLine($"C10d/e type {r.Code} ({n.DisplayName}): raw {r.Headers} headers / {r.Lines} lines / {r.Value}; new {n.HeaderCount} / {n.LineCount} / {n.TotalValue}");
            Assert.Equal(r.Code, n.Code);
            Assert.Equal(r.Headers, n.HeaderCount);
            Assert.Equal(r.Lines, n.LineCount);
            Assert.Equal(r.Value, n.TotalValue);
        }
        output.WriteLine($"C10i header type codes without RCV_TYPE row: {orphanTypes} (they still appear with 'ไม่พบชื่อประเภท')");
        Assert.Equal(report.HeaderCount, report.TypeBreakdown.Sum(t => t.HeaderCount));
    }

    [SkippableFact]
    public async Task C10f_fiscal_year_grouping_matches_raw_date_rule()
    {
        var connections = db.RequireOrSkip();
        var repo = new NonPoReceiptRepository(connections);
        await using var c = await connections.OpenAsync();

        var raw = (await c.QueryAsync<(int Fy, int N, DateTime Min, DateTime Max)>(ReadOnlySql.Ensure(
            $"SELECT {FyExpr}, COUNT(*), MIN(h.DATE_RECEIVE), MAX(h.DATE_RECEIVE) FROM dbo.OTH_IVO h GROUP BY {FyExpr} ORDER BY 1 DESC"))).ToList();
        var years = await repo.GetFiscalYearsWithDataAsync();

        Assert.Equal(raw.Select(r => r.Fy), years);
        foreach (var r in raw)
        {
            var headers = await repo.GetHeadersAsync(r.Fy, null);
            output.WriteLine($"C10f FY{r.Fy}: raw {r.N} ({r.Min:d} … {r.Max:d}), new {headers.Count}");
            Assert.Equal(r.N, headers.Count);
            Assert.All(headers, h => Assert.Equal(r.Fy, h.FiscalYear));
            Assert.All(headers, h => Assert.InRange(h.DateReceive!.Value, r.Min, r.Max));
        }
    }

    [SkippableFact]
    public async Task C10g_C10j_representative_headers_match_raw_and_detail_header_equals_list_row()
    {
        var (fy, report, repo) = await LoadAsync();
        Skip.If(report.HeaderCount == 0, $"no receipts in FY{fy}");
        var connections = db.RequireOrSkip();
        await using var c = await connections.OpenAsync();

        var picks = report.Headers.GroupBy(h => h.TypeCode).Select(g => g.First()).Take(4).ToList();
        foreach (var h in picks)
        {
            var raw = await c.QuerySingleAsync<RawHeader>(ReadOnlySql.Ensure("""
                SELECT h.RECEIVE_NO, h.INVOICE_NO, h.INVOICE_DATE, h.DATE_RECEIVE, RTRIM(h.RCV_TYPE) AS RCV_TYPE, h.DPT_CODE, h.TOTAL_ITEM, h.TOTAL_VALUE, h.OTH_NOTE,
                       (SELECT RCV_NAME FROM dbo.RCV_TYPE t WHERE t.RCV_TYPE_CODE = RTRIM(h.RCV_TYPE)) AS TypeName,
                       (SELECT TOP 1 COMPANY_NAME FROM dbo.COMPANY c WHERE c.COMPANY_CODE = h.DPT_CODE ORDER BY c.RECORD_NUMBER) AS SourceName,
                       (SELECT COUNT(*) FROM dbo.OTH_IVOC c WHERE c.RECEIVE_NO = h.RECEIVE_NO) AS LineCount,
                       (SELECT ISNULL(SUM(QTY_ORDER), 0) FROM dbo.OTH_IVOC c WHERE c.RECEIVE_NO = h.RECEIVE_NO) AS LineQty
                FROM dbo.OTH_IVO h WHERE h.RECEIVE_NO = @R
                """), new { R = h.ReceiveNo });

            output.WriteLine($"C10g {raw.RECEIVE_NO} type {raw.RCV_TYPE} {raw.TypeName} src {raw.DPT_CODE} {raw.SourceName} date {raw.DATE_RECEIVE:d} items {raw.TOTAL_ITEM} value {raw.TOTAL_VALUE} lines {raw.LineCount}");
            Assert.Equal(raw.INVOICE_NO, h.InvoiceNo);
            Assert.Equal(raw.INVOICE_DATE, h.InvoiceDate);
            Assert.Equal(raw.DATE_RECEIVE, h.DateReceive);
            Assert.Equal(raw.RCV_TYPE, h.TypeCode);
            Assert.Equal(raw.TypeName, h.TypeName);
            Assert.Equal(raw.DPT_CODE, h.SourceCode);
            Assert.Equal(raw.SourceName, h.SourceName);
            Assert.Equal(raw.TOTAL_ITEM, h.TotalItem);
            Assert.Equal(raw.TOTAL_VALUE, h.TotalValue);
            Assert.Equal(raw.OTH_NOTE, h.Note);
            Assert.Equal(raw.LineCount, h.LineCount);
            Assert.Equal(raw.LineQty, h.LineQtySum);

            // C10j: the detail page header is the same authoritative row as the list row.
            var detail = await repo.GetDetailAsync(h.ReceiveNo);
            Assert.NotNull(detail);
            Assert.Equal(h, detail!.Header);
        }
    }

    [SkippableFact]
    public async Task C10h_representative_lines_and_reconciliation_hold_for_every_header()
    {
        var (fy, report, repo) = await LoadAsync();
        Skip.If(report.HeaderCount == 0, $"no receipts in FY{fy}");
        var connections = db.RequireOrSkip();
        await using var c = await connections.OpenAsync();

        // Verified value identity: TOTAL_VALUE = SUM(UNIT_VALUE * QTY_ORDER / PACK_RATIO); TOTAL_ITEM = line count.
        var rawIdentity = await c.QuerySingleAsync<(int Headers, int ValueOk, int ItemOk)>(ReadOnlySql.Ensure($"""
            SELECT COUNT(*),
                   SUM(CASE WHEN ABS(ISNULL(x.TOTAL_VALUE,0) - ISNULL(x.LineValue,0)) < 0.005 THEN 1 ELSE 0 END),
                   SUM(CASE WHEN ISNULL(x.TOTAL_ITEM,0) = x.LineCount THEN 1 ELSE 0 END)
            FROM (SELECT h.TOTAL_VALUE, h.TOTAL_ITEM,
                         (SELECT SUM(CASE WHEN c.PACK_RATIO > 0 THEN c.UNIT_VALUE * c.QTY_ORDER / c.PACK_RATIO END) FROM dbo.OTH_IVOC c WHERE c.RECEIVE_NO = h.RECEIVE_NO) AS LineValue,
                         (SELECT COUNT(*) FROM dbo.OTH_IVOC c WHERE c.RECEIVE_NO = h.RECEIVE_NO) AS LineCount
                  FROM dbo.OTH_IVO h WHERE {FyExpr} = @Fy) x
            """), new { Fy = fy });
        output.WriteLine($"C10h FY{fy}: headers {rawIdentity.Headers}, value identity holds {rawIdentity.ValueOk}, item-count identity holds {rawIdentity.ItemOk}");

        var inconsistent = new List<string>();
        var sample = report.Headers.Take(12).ToList();
        foreach (var h in sample)
        {
            var d = await repo.GetDetailAsync(h.ReceiveNo);
            Assert.NotNull(d);
            foreach (var l in d!.Lines)
            {
                Assert.False(string.IsNullOrEmpty(l.WorkingCode));
                if (l.PackRatio is > 0) { Assert.Equal(l.QtyOrder / l.PackRatio, l.Packs); } else { Assert.Null(l.Packs); }
                Assert.Equal(l.DrugName is null ? $"{l.WorkingCode} (ไม่พบชื่อรายการ)" : l.DrugName, l.DisplayName);
            }
            if (!d.Reconciliation.IsConsistent) { inconsistent.Add($"{h.ReceiveNo}: Δ{d.Reconciliation.ValueDelta} items {d.Reconciliation.HeaderItemCount}/{d.Reconciliation.LineCount}"); }
        }
        output.WriteLine($"C10h sampled {sample.Count} headers; inconsistent: {inconsistent.Count} {string.Join("; ", inconsistent)}");
        if (rawIdentity.ValueOk == rawIdentity.Headers && rawIdentity.ItemOk == rawIdentity.Headers)
        {
            Assert.Empty(inconsistent);   // the app's reconciliation must agree with the raw identity when it holds everywhere
        }
    }

    [SkippableFact]
    public async Task C10i_data_loss_guards_missing_item_names_and_unknown_keys()
    {
        var connections = db.RequireOrSkip();
        var repo = new NonPoReceiptRepository(connections);
        await using var c = await connections.OpenAsync();

        var linesWithoutItem = await c.ExecuteScalarAsync<int>(ReadOnlySql.Ensure(
            "SELECT COUNT(*) FROM dbo.OTH_IVOC c WHERE NOT EXISTS (SELECT 1 FROM dbo.INV_MD m WHERE m.WORKING_CODE = c.WORKING_CODE)"));
        var duplicateSourceCodes = await c.ExecuteScalarAsync<int>(ReadOnlySql.Ensure(
            "SELECT COUNT(*) FROM dbo.OTH_IVO h WHERE (SELECT COUNT(*) FROM dbo.COMPANY c WHERE c.COMPANY_CODE = h.DPT_CODE) > 1"));
        var rawLines = await c.ExecuteScalarAsync<int>(ReadOnlySql.Ensure("SELECT COUNT(*) FROM dbo.OTH_IVOC"));

        // Query-shape check for a missing INV_MD item via table-value constructor (nothing written).
        var shape = (await c.QueryAsync<(string Code, string? Name)>(ReadOnlySql.Ensure("""
            SELECT c.WORKING_CODE, d.DRUG_NAME FROM (VALUES (N'1000010'), (N'ZZZ9999')) AS c(WORKING_CODE)
            OUTER APPLY (SELECT TOP 1 m.DRUG_NAME FROM dbo.INV_MD m WHERE m.WORKING_CODE = c.WORKING_CODE ORDER BY m.RECORD_NUMBER) d
            ORDER BY c.WORKING_CODE
            """))).ToList();
        Assert.Equal(2, shape.Count);
        Assert.NotNull(shape[0].Name);
        Assert.Null(shape[1].Name);              // row preserved, name missing

        output.WriteLine($"C10i lines without INV_MD item: {linesWithoutItem}; headers whose DPT_CODE is duplicated in COMPANY: {duplicateSourceCodes}; total lines {rawLines}");
        Assert.Null(await repo.GetDetailAsync("ZZZZ9999"));
        Assert.Null(await repo.GetDetailAsync("O69'; --"));
        Assert.Null(await repo.GetDetailAsync(new string('9', 11)));
    }

    private sealed record RawHeader(string RECEIVE_NO, string? INVOICE_NO, DateTime? INVOICE_DATE, DateTime? DATE_RECEIVE, string? RCV_TYPE, string? DPT_CODE,
        decimal? TOTAL_ITEM, decimal? TOTAL_VALUE, string? OTH_NOTE, string? TypeName, string? SourceName, int LineCount, decimal LineQty);
}
