using Dapper;
using Invc.Core.PurchaseOrders;
using Invc.Infrastructure.Data;
using Invc.Infrastructure.PurchaseOrders;
using Xunit.Abstractions;

namespace Invc.IntegrationTests;

/// <summary>
/// Phase 4 parity checks C1–C9 (docs/report-parity-matrix.md) — read-only against live INV.
/// The raw side is the legacy SQL (PO_Search.asp / Dashboard.asp / table_ipiss.asp) with the fiscal-year prefix
/// parameterised. No fixtures; zero rows are valid results.
/// </summary>
public class PurchaseOrderParityTests(ProductionReadOnlyFixture db, ITestOutputHelper output) : IClassFixture<ProductionReadOnlyFixture>
{
    private const string LegacyJoin = """
        FROM TBLBUY INNER JOIN (TblPOStatus INNER JOIN (BDG_TYPE INNER JOIN (TBLED_NED INNER JOIN (COMPANY INNER JOIN MS_PO
             ON COMPANY.COMPANY_CODE = MS_PO.VENDOR_CODE) ON TBLED_NED.EDCODE = MS_PO.ED_NED) ON BDG_TYPE.BDGCODE = MS_PO.BUDGET_TYPE)
             ON TblPOStatus.StatusCode = MS_PO.STATUS) ON TBLBUY.BUYCODE = MS_PO.BUY_METHOD
        """;

    private async Task<(int fy, PurchaseOrderReport report, PurchaseOrderRepository repo)> LoadAsync(PurchaseOrderBucket bucket)
    {
        var connections = db.RequireOrSkip();
        var repo = new PurchaseOrderRepository(connections);
        var open = await repo.GetOpenBudgetYearsAsync();
        var years = await repo.GetPoFiscalYearsAsync();
        var fy = DefaultFiscalYear.Choose(open, years, DateTime.Today, out var note);
        output.WriteLine($"{DateTime.Now:O} fiscal year {fy} (open budget years: {string.Join(",", open)}; PO years: {string.Join(",", years)}; note: {note ?? "-"})");
        var headers = await repo.GetHeadersAsync(fy, null);
        return (fy, PurchaseOrderReport.Build(fy, headers, bucket), repo);
    }

    [SkippableFact]
    public async Task C1_C2_issued_count_and_value_match_legacy()
    {
        var (fy, report, _) = await LoadAsync(PurchaseOrderBucket.Issued);
        var connections = db.RequireOrSkip();
        await using var c = await connections.OpenAsync();
        var raw = await c.QuerySingleAsync<(int Count, decimal? Value)>(ReadOnlySql.Ensure(
            "SELECT COUNT(*), SUM(TOTAL_COST) FROM dbo.MS_PO WHERE LEFT(PO_NO,2) = @P AND STATUS NOT IN ('0','C')"),
            new { P = PurchaseOrderRules.PoPrefix(fy) });

        var issued = report.Buckets[PurchaseOrderBucket.Issued];
        output.WriteLine($"C1/C2 FY{fy}: legacy {raw.Count} / {raw.Value ?? 0m}; new {issued.Count} / {issued.TotalValue}");
        Assert.Equal(raw.Count, issued.Count);
        Assert.Equal(raw.Value ?? 0m, issued.TotalValue);
        Assert.Equal(issued.Count, report.Rows.Count);
    }

    [SkippableFact]
    public async Task C3_bucket_counts_and_values_match_documented_status_sets()
    {
        var (fy, report, _) = await LoadAsync(PurchaseOrderBucket.Issued);
        var connections = db.RequireOrSkip();
        await using var c = await connections.OpenAsync();
        var prefix = PurchaseOrderRules.PoPrefix(fy);

        var sets = new Dictionary<PurchaseOrderBucket, string>
        {
            [PurchaseOrderBucket.Received] = "('2','3','4','5','6','7','8','9','D')",
            [PurchaseOrderBucket.Accounting] = "('4','5','6','7','8','9','D')",
            [PurchaseOrderBucket.Finance] = "('4','5','7','8','9')",
            [PurchaseOrderBucket.Closed] = "('5','9')",
        };
        foreach (var (bucket, set) in sets)
        {
            var raw = await c.QuerySingleAsync<(int Count, decimal? Value)>(ReadOnlySql.Ensure(
                $"SELECT COUNT(*), SUM(TOTAL_COST) FROM dbo.MS_PO WHERE LEFT(PO_NO,2) = @P AND STATUS IN {set}"), new { P = prefix });
            var agg = report.Buckets[bucket];
            output.WriteLine($"C3 {bucket}: legacy {raw.Count} / {raw.Value ?? 0m}; new {agg.Count} / {agg.TotalValue}");
            Assert.Equal(raw.Count, agg.Count);
            Assert.Equal(raw.Value ?? 0m, agg.TotalValue);
        }
    }

    [SkippableFact]
    public async Task C4_per_status_breakdown_matches_authoritative_ms_po()
    {
        var (fy, report, _) = await LoadAsync(PurchaseOrderBucket.All);
        var connections = db.RequireOrSkip();
        await using var c = await connections.OpenAsync();
        var raw = (await c.QueryAsync<(string? Status, int Count, decimal? Value)>(ReadOnlySql.Ensure(
            "SELECT STATUS, COUNT(*), SUM(TOTAL_COST) FROM dbo.MS_PO WHERE LEFT(PO_NO,2) = @P GROUP BY STATUS ORDER BY STATUS"),
            new { P = PurchaseOrderRules.PoPrefix(fy) })).ToList();

        Assert.Equal(raw.Count, report.StatusBreakdown.Count);
        foreach (var (r, n) in raw.Zip(report.StatusBreakdown))
        {
            output.WriteLine($"C4 status {r.Status}: legacy {r.Count} / {r.Value ?? 0m}; new {n.Count} / {n.TotalValue} ({n.Label ?? "no label"})");
            Assert.Equal((r.Status ?? string.Empty).Trim(), n.Key);
            Assert.Equal(r.Count, n.Count);
            Assert.Equal(r.Value ?? 0m, n.TotalValue);
        }
    }

    [SkippableFact]
    public async Task C5_representative_header_fields_match_raw_row()
    {
        var (fy, report, repo) = await LoadAsync(PurchaseOrderBucket.All);
        Skip.If(report.Orders.Count == 0, $"no purchase orders in FY{fy}");
        var connections = db.RequireOrSkip();
        await using var c = await connections.OpenAsync();

        // Discover the representative record read-only: the first PO of the year in REAL_PO order.
        var raw = await c.QuerySingleAsync<RawHeader>(ReadOnlySql.Ensure("""
            SELECT TOP 1 p.PO_NO, p.REAL_PO, p.PO_DATE, p.DOC_NO, p.STATUS, p.VENDOR_CODE, p.BUDGET_TYPE, p.BUY_METHOD, p.ED_NED,
                   p.TOTAL_ITEM, p.TOTAL_COST, p.FIRST_RCVDATE, p.BILLIN, p.BILLOUT, p.BILLEND, p.BILLOUTACC, p.BILLOUTFIN, p.BILL_END_ACC, p.APPROVE_NO_FIN, p.BILL_PAY_FIN,
                   (SELECT StatusName FROM dbo.TblPOStatus s WHERE s.StatusCode = p.STATUS) AS StatusName,
                   (SELECT BDGNAME FROM dbo.BDG_TYPE b WHERE b.BDGCODE = p.BUDGET_TYPE) AS BudgetName,
                   (SELECT BUYNAME FROM dbo.TBLBUY t WHERE t.BUYCODE = p.BUY_METHOD) AS BuyName,
                   (SELECT TOP 1 COMPANY_NAME_PO FROM dbo.COMPANY v WHERE v.COMPANY_CODE = p.VENDOR_CODE ORDER BY v.RECORD_NUMBER) AS VendorName
            FROM dbo.MS_PO p WHERE LEFT(p.PO_NO,2) = @P ORDER BY p.REAL_PO, p.PO_NO
            """), new { P = PurchaseOrderRules.PoPrefix(fy) });

        var n = report.Orders[0];
        output.WriteLine($"C5 {raw.REAL_PO} ({raw.PO_NO}) {raw.PO_DATE:d} vendor {raw.VENDOR_CODE} {raw.VendorName} status {raw.STATUS} {raw.StatusName} items {raw.TOTAL_ITEM} cost {raw.TOTAL_COST}");
        Assert.Equal(raw.PO_NO, n.PoNo);
        Assert.Equal(raw.REAL_PO, n.RealPo);
        Assert.Equal(raw.PO_DATE, n.PoDate);
        Assert.Equal(raw.DOC_NO, n.DocNo);
        Assert.Equal(raw.STATUS, n.Status);
        Assert.Equal(raw.StatusName, n.StatusName);
        Assert.Equal(raw.VENDOR_CODE, n.VendorCode);
        Assert.Equal(raw.VendorName, n.VendorName);
        Assert.Equal(raw.BUDGET_TYPE, n.BudgetType);
        Assert.Equal(raw.BudgetName, n.BudgetName);
        Assert.Equal(raw.BUY_METHOD, n.BuyMethod);
        Assert.Equal(raw.BuyName, n.BuyMethodName);
        Assert.Equal(raw.ED_NED, n.EdNed);
        Assert.Equal(raw.TOTAL_ITEM, n.TotalItem);
        Assert.Equal(raw.TOTAL_COST, n.TotalCost);
        Assert.Equal(raw.FIRST_RCVDATE, n.FirstReceiveDate);
        Assert.Equal(raw.BILLIN, n.BillIn);
        Assert.Equal(raw.BILLOUT, n.BillOut);
        Assert.Equal(raw.BILLEND, n.BillEnd);
        Assert.Equal(raw.BILLOUTACC, n.BillOutAcc);
        Assert.Equal(raw.BILLOUTFIN, n.BillOutFin);
        Assert.Equal(raw.BILL_END_ACC, n.BillEndAcc);
        Assert.Equal(raw.APPROVE_NO_FIN, n.ApproveNoFin);
        Assert.Equal(raw.BILL_PAY_FIN, n.BillPayFin);
        Assert.Equal(fy, n.FiscalYear);

        // Detail by REAL_PO returns the same header.
        Skip.If(string.IsNullOrWhiteSpace(raw.REAL_PO), "representative PO has no REAL_PO");
        var detail = await repo.GetDetailAsync(raw.REAL_PO!);
        Assert.NotNull(detail);
        Assert.Equal(n.PoNo, detail!.Header.PoNo);
    }

    [SkippableFact]
    public async Task C6_lines_match_legacy_PODetail_query_and_reconcile_with_header()
    {
        var (fy, report, repo) = await LoadAsync(PurchaseOrderBucket.All);
        var po = report.Orders.FirstOrDefault(o => !string.IsNullOrWhiteSpace(o.RealPo));
        Skip.If(po is null, $"no purchase orders with REAL_PO in FY{fy}");
        var connections = db.RequireOrSkip();
        await using var c = await connections.OpenAsync();

        // Legacy PODetail.asp line query: MS_PO_C.PO_NO = MS_PO.PO_NO (INNER JOIN INV_MD).
        var raw = (await c.QueryAsync<RawLine>(ReadOnlySql.Ensure("""
            SELECT MS_PO_C.WORKING_CODE, INV_MD.DRUG_NAME, MS_PO_C.QTY_ORDER, MS_PO_C.PACK_RATIO1, MS_PO_C.PO_UNIT, MS_PO_C.BUY_UNIT_COST, MS_PO_C.BUY_VALUE, MS_PO_C.QTY_FREE, MS_PO_C.PACK_RATIO2
            FROM INV_MD INNER JOIN MS_PO_C ON INV_MD.WORKING_CODE = MS_PO_C.WORKING_CODE WHERE MS_PO_C.PO_NO = @PoNo ORDER BY MS_PO_C.RUNNO, MS_PO_C.RECORD_NUMBER
            """), new { PoNo = po!.PoNo })).ToList();
        var rawCountAll = await c.ExecuteScalarAsync<int>(ReadOnlySql.Ensure("SELECT COUNT(*) FROM dbo.MS_PO_C WHERE PO_NO = @PoNo"), new { PoNo = po.PoNo });

        var detail = await repo.GetDetailAsync(po.RealPo!);
        Assert.NotNull(detail);
        var rec = detail!.Reconciliation;
        output.WriteLine($"C6 {po.RealPo}: legacy lines {raw.Count} (all MS_PO_C rows {rawCountAll}), new {detail.Lines.Count}; header items {rec.HeaderItemCount} cost {rec.HeaderTotal}; line total {rec.LineTotal}; consistent {rec.IsConsistent}");

        Assert.Equal(rawCountAll, detail.Lines.Count);      // authoritative MS_PO_C count (legacy INNER JOIN could lose lines)
        Assert.Equal(raw.Count, detail.Lines.Count(l => l.DrugName is not null));
        foreach (var (r, n) in raw.Zip(detail.Lines))
        {
            Assert.Equal(r.WORKING_CODE, n.WorkingCode);
            Assert.Equal(r.DRUG_NAME, n.DrugName);
            Assert.Equal(r.QTY_ORDER, n.QtyOrder);
            Assert.Equal(r.PACK_RATIO1, n.PackRatio1);
            Assert.Equal(r.PACK_RATIO1 is > 0 ? r.QTY_ORDER / r.PACK_RATIO1 : null, n.PacksOrdered);
            Assert.Equal(r.PO_UNIT, n.PoUnit);
            Assert.Equal(r.BUY_UNIT_COST, n.BuyUnitCost);
            Assert.Equal(r.BUY_VALUE, n.BuyValue);
            Assert.Equal(r.QTY_FREE, n.QtyFree);
        }

        var rawLineTotal = await c.ExecuteScalarAsync<decimal?>(ReadOnlySql.Ensure("SELECT SUM(BUY_VALUE) FROM dbo.MS_PO_C WHERE PO_NO = @PoNo"), new { PoNo = po.PoNo });
        Assert.Equal(rawLineTotal ?? 0m, rec.LineTotal);
        if (!rec.IsConsistent)
        {
            output.WriteLine($"  NOTE: header/line discrepancy on {po.RealPo}: Δcost {rec.TotalDelta}, items {rec.HeaderItemCount} vs {rec.LineCount}");
        }
    }

    [SkippableFact]
    public async Task C7_receipt_count_uses_REAL_PO_relationship()
    {
        var (fy, report, repo) = await LoadAsync(PurchaseOrderBucket.All);
        var po = report.Orders.FirstOrDefault(o => !string.IsNullOrWhiteSpace(o.RealPo));
        Skip.If(po is null, $"no purchase orders with REAL_PO in FY{fy}");
        var connections = db.RequireOrSkip();
        await using var c = await connections.OpenAsync();

        var byRealPo = await c.ExecuteScalarAsync<int>(ReadOnlySql.Ensure("SELECT COUNT(*) FROM dbo.MS_IVO WHERE PO_NO = @RealPo"), new { RealPo = po!.RealPo });
        var byPoNo = await c.ExecuteScalarAsync<int>(ReadOnlySql.Ensure("SELECT COUNT(*) FROM dbo.MS_IVO WHERE PO_NO = @PoNo"), new { PoNo = po.PoNo });
        var total = await c.ExecuteScalarAsync<int>(ReadOnlySql.Ensure("SELECT COUNT(*) FROM dbo.MS_IVO"));
        var detail = await repo.GetDetailAsync(po.RealPo!);

        output.WriteLine($"C7 {po.RealPo}: MS_IVO by REAL_PO {byRealPo}, by PO_NO {byPoNo}, table total {total}; new {detail!.Receipts.Count}");
        Assert.Equal(byRealPo, detail.Receipts.Count);
    }

    [SkippableFact]
    public async Task Receipt_query_shape_holds_on_synthetic_rows_without_writes()
    {
        var connections = db.RequireOrSkip();
        await using var c = await connections.OpenAsync();
        // MS_IVO substitute: two receipts for REAL_PO 'K1' (one with NULL dates), one for another PO, one keyed by the internal PO_NO (must NOT match).
        var ivo = "(VALUES (1, N'R1', N'K1', N'INV-1', CAST('2026-01-05' AS datetime), CAST('2026-01-06' AS datetime), 100.0, 2, NULL), " +
                  "(2, N'R2', N'K1', NULL, NULL, NULL, NULL, NULL, NULL), (3, N'R3', N'K2', NULL, NULL, NULL, NULL, NULL, NULL), (4, N'R4', N'1', NULL, NULL, NULL, NULL, NULL, NULL)) " +
                  "AS r(RECORD_NUMBER, RECEIVE_NO, PO_NO, INVOICE_NO, INVOICE_DATE, DATE_RECEIVE, TOTAL_COST, TOTAL_ITEM, DATE_ACC)";
        var ivoc = "(VALUES (1, N'R1', N'1000010', 500, 100, 5.0, NULL, NULL, NULL, NULL, N'L1', NULL), (2, N'R1', N'1000020', 7, 0, 1.0, NULL, NULL, NULL, NULL, NULL, NULL), " +
                   "(3, N'R9', N'1000030', 1, 1, 1.0, NULL, NULL, NULL, NULL, NULL, NULL)) " +
                   "AS c(RECORD_NUMBER, RECEIVE_NO, WORKING_CODE, QTY_ORDER, PACK_RATIO1, BUY_UNIT_COST, QTY_FREE, PACK_RATIO2, EXPIRED_DATE1, LOCATION1, LOTNO, MANUFAC_CODE)";

        var receipts = (await c.QueryAsync<PurchaseOrderRepository.ReceiptRow>(ReadOnlySql.Ensure(PurchaseOrderSql.Receipts(ivo)), new { RealPo = "K1" })).ToList();
        var lines = (await c.QueryAsync<PurchaseOrderRepository.ReceiptLineRow>(ReadOnlySql.Ensure(PurchaseOrderSql.ReceiptLines(ivo, ivoc)), new { RealPo = "K1" })).ToList();
        var assembled = PurchaseOrderRepository.AssembleReceipts(receipts, lines);

        // NULL DATE_RECEIVE sorts first in SQL Server, so compare as a set and look receipts up by number.
        Assert.Equal(new[] { "R1", "R2" }, assembled.Select(r => r.ReceiveNo).OrderBy(x => x));   // K2 and the PO_NO-keyed row are excluded
        var r1 = assembled.Single(r => r.ReceiveNo == "R1");
        var r2 = assembled.Single(r => r.ReceiveNo == "R2");
        Assert.Equal(2, r1.Lines.Count);                                              // only RECEIVE_NO = R1 lines (R9 orphan excluded)
        Assert.Equal(5m, r1.Lines[0].PacksReceived);
        Assert.Null(r1.Lines[1].PacksReceived);                                       // pack ratio 0 → no division
        Assert.Null(r2.DateReceive);                                                  // null dates survive
        Assert.Empty(r2.Lines);
        Assert.Equal("Acyclovir 400 mg tab", r1.Lines[0].DrugName);         // drug name resolved from INV_MD (read-only)

        var none = (await c.QueryAsync<PurchaseOrderRepository.ReceiptRow>(ReadOnlySql.Ensure(PurchaseOrderSql.Receipts(ivo)), new { RealPo = "NOPE" })).ToList();
        Assert.Empty(none);
    }

    [SkippableFact]
    public async Task C8_join_loss_guard_legacy_inner_join_vs_authoritative_and_safe_lookup()
    {
        var (fy, report, _) = await LoadAsync(PurchaseOrderBucket.Issued);
        var connections = db.RequireOrSkip();
        await using var c = await connections.OpenAsync();
        var prefix = PurchaseOrderRules.PoPrefix(fy);

        var legacyInner = await c.ExecuteScalarAsync<int>(ReadOnlySql.Ensure(
            $"SELECT COUNT(*) {LegacyJoin} WHERE LEFT(MS_PO.PO_NO,2) = @P AND MS_PO.STATUS NOT IN ('0','C')"), new { P = prefix });
        var authoritative = await c.ExecuteScalarAsync<int>(ReadOnlySql.Ensure(
            "SELECT COUNT(*) FROM dbo.MS_PO WHERE LEFT(PO_NO,2) = @P AND STATUS NOT IN ('0','C')"), new { P = prefix });
        var orphans = (await c.QueryAsync<(string PoNo, string? Vendor, string? Status, string? Budget, string? Buy, string? Ed)>(ReadOnlySql.Ensure("""
            SELECT p.PO_NO,
                   CASE WHEN NOT EXISTS (SELECT 1 FROM dbo.COMPANY c WHERE c.COMPANY_CODE = p.VENDOR_CODE) THEN p.VENDOR_CODE END,
                   CASE WHEN NOT EXISTS (SELECT 1 FROM dbo.TblPOStatus s WHERE s.StatusCode = p.STATUS) THEN p.STATUS END,
                   CASE WHEN NOT EXISTS (SELECT 1 FROM dbo.BDG_TYPE b WHERE b.BDGCODE = p.BUDGET_TYPE) THEN p.BUDGET_TYPE END,
                   CASE WHEN NOT EXISTS (SELECT 1 FROM dbo.TBLBUY t WHERE t.BUYCODE = p.BUY_METHOD) THEN p.BUY_METHOD END,
                   CASE WHEN NOT EXISTS (SELECT 1 FROM dbo.TBLED_NED e WHERE e.EDCODE = p.ED_NED) THEN p.ED_NED END
            FROM dbo.MS_PO p WHERE LEFT(p.PO_NO,2) = @P AND p.STATUS NOT IN ('0','C')
            """), new { P = prefix })).Where(o => o.Vendor is not null || o.Status is not null || o.Budget is not null || o.Buy is not null || o.Ed is not null).ToList();
        var duplicateVendorCodes = await c.ExecuteScalarAsync<int>(ReadOnlySql.Ensure("""
            SELECT COUNT(*) FROM dbo.MS_PO p WHERE LEFT(p.PO_NO,2) = @P
              AND (SELECT COUNT(*) FROM dbo.COMPANY c WHERE c.COMPANY_CODE = p.VENDOR_CODE) > 1
            """), new { P = prefix });

        var safe = report.Buckets[PurchaseOrderBucket.Issued].Count;
        output.WriteLine($"C8 FY{fy}: legacy INNER JOIN {legacyInner}, authoritative MS_PO {authoritative}, new safe lookup {safe}; orphan lookup codes: {orphans.Count}; POs whose vendor code is duplicated in COMPANY: {duplicateVendorCodes}");
        foreach (var o in orphans)
        {
            output.WriteLine($"  orphan on {o.PoNo}: vendor={o.Vendor} status={o.Status} budget={o.Budget} buy={o.Buy} ed={o.Ed}");
        }

        Assert.Equal(authoritative, safe);                       // the new list never loses (or multiplies) a PO
        if (orphans.Count == 0 && duplicateVendorCodes == 0)
        {
            Assert.Equal(authoritative, legacyInner);            // no lookup defects → legacy and new agree
        }
        else
        {
            Assert.NotEqual(authoritative, legacyInner);         // documented legacy defect: INNER JOIN loses/multiplies rows
        }
    }

    [SkippableFact]
    public async Task C9_distinct_billout_dates_match_legacy_date_list()
    {
        var (fy, _, repo) = await LoadAsync(PurchaseOrderBucket.Issued);
        var connections = db.RequireOrSkip();
        await using var c = await connections.OpenAsync();
        var legacy = (await c.QueryAsync<DateTime>(ReadOnlySql.Ensure(
            "SELECT DISTINCT BILLOUT FROM MS_PO WHERE LEFT([PO_NO],2) = @P AND BILLOUT IS NOT NULL ORDER BY BILLOUT DESC"),
            new { P = PurchaseOrderRules.PoPrefix(fy) })).Select(d => d.Date).Distinct().ToList();
        var dates = await repo.GetBillOutDatesAsync(fy);
        output.WriteLine($"C9 FY{fy}: legacy distinct BILLOUT dates {legacy.Count}, new {dates.Count}");
        Assert.Equal(legacy, dates.Select(d => d.Date));
    }

    [SkippableFact]
    public async Task Unknown_or_malformed_real_po_returns_null()
    {
        var connections = db.RequireOrSkip();
        var repo = new PurchaseOrderRepository(connections);
        Assert.Null(await repo.GetDetailAsync("ZZZZ9999"));
        Assert.Null(await repo.GetDetailAsync("K69'; --"));
        Assert.Null(await repo.GetDetailAsync(new string('9', 11)));
    }

    private sealed record RawHeader(string PO_NO, string? REAL_PO, DateTime? PO_DATE, string? DOC_NO, string? STATUS, string? VENDOR_CODE, string? BUDGET_TYPE,
        string? BUY_METHOD, string? ED_NED, decimal? TOTAL_ITEM, decimal? TOTAL_COST, DateTime? FIRST_RCVDATE, DateTime? BILLIN, DateTime? BILLOUT, DateTime? BILLEND,
        DateTime? BILLOUTACC, DateTime? BILLOUTFIN, DateTime? BILL_END_ACC, string? APPROVE_NO_FIN, DateTime? BILL_PAY_FIN, string? StatusName, string? BudgetName, string? BuyName, string? VendorName);

    private sealed record RawLine(string WORKING_CODE, string? DRUG_NAME, decimal? QTY_ORDER, decimal? PACK_RATIO1, string? PO_UNIT, decimal? BUY_UNIT_COST, decimal? BUY_VALUE, decimal? QTY_FREE, decimal? PACK_RATIO2);
}
