using Dapper;
using Invc.Core.Inventory;
using Invc.Infrastructure.Data;
using Invc.Infrastructure.Inventory;
using Xunit.Abstractions;

namespace Invc.IntegrationTests;

/// <summary>
/// Phase 2 parity checks A2–A10 (docs/report-parity-matrix.md) — read-only, executed against live INV.
/// Legacy/raw SQL and the new repository run back-to-back on the same connection factory; no baseline
/// numbers are hard-coded because production changes daily.
/// </summary>
public class InventoryPhase2ParityTests(ProductionReadOnlyFixture db, ITestOutputHelper output) : IClassFixture<ProductionReadOnlyFixture>
{
    private const string ActiveFilter = "WHERE [NOUSE] IS NULL";

    [SkippableFact]
    public async Task A2_A3_summary_totals_and_A4_ed_ned_groups_match_raw_sql()
    {
        var connections = db.RequireOrSkip();
        var repo = new InventoryRepository(connections);

        await using var c = await connections.OpenAsync();
        var raw = await c.QuerySingleAsync<(int Count, decimal Qty, decimal Value)>(ReadOnlySql.Ensure(
            $"SELECT COUNT(*), ISNULL(SUM(QTY_ON_HAND),0), ISNULL(SUM(TOTAL_VALUE),0) FROM dbo.INV_MD {ActiveFilter}"));
        var rawGroups = (await c.QueryAsync<(string? Code, int Count, decimal Qty, decimal Value)>(ReadOnlySql.Ensure(
            $"SELECT ED_NED, COUNT(*), ISNULL(SUM(QTY_ON_HAND),0), ISNULL(SUM(TOTAL_VALUE),0) FROM dbo.INV_MD {ActiveFilter} GROUP BY ED_NED ORDER BY ED_NED"))).ToList();

        var summary = await repo.GetSummaryAsync();

        output.WriteLine($"{DateTime.Now:O} A1/A2/A3 raw: {raw.Count} / {raw.Qty} / {raw.Value}  new: {summary.ActiveItemCount} / {summary.TotalQtyOnHand} / {summary.TotalValue}");
        Assert.Equal(raw.Count, summary.ActiveItemCount);
        Assert.Equal(raw.Qty, summary.TotalQtyOnHand);
        Assert.Equal(raw.Value, summary.TotalValue);

        Assert.Equal(rawGroups.Count, summary.Groups.Count);
        foreach (var (rg, ng) in rawGroups.Zip(summary.Groups))
        {
            output.WriteLine($"A4 {rg.Code}: raw {rg.Count}/{rg.Qty}/{rg.Value} new {ng.ItemCount}/{ng.TotalQtyOnHand}/{ng.TotalValue} ({ng.EdNedShortName})");
            Assert.Equal(rg.Code, ng.EdNedCode);
            Assert.Equal(rg.Count, ng.ItemCount);
            Assert.Equal(rg.Qty, ng.TotalQtyOnHand);
            Assert.Equal(rg.Value, ng.TotalValue);
        }
    }

    [SkippableFact]
    public async Task A5_representative_items_match_legacy_row_values()
    {
        var connections = db.RequireOrSkip();
        var repo = new InventoryRepository(connections);

        await using var c = await connections.OpenAsync();
        // Legacy INV_Status.asp row source (explicit columns instead of *; same filter and legacy order).
        var legacy = (await c.QueryAsync<LegacyRow>(ReadOnlySql.Ensure($"""
            SELECT WORKING_CODE, DRUG_NAME, HOSP_CODE, VEN, QTY_ON_HAND, SALE_UNIT, LOCATION, RATE_PER_MONTH
            FROM dbo.INV_MD {ActiveFilter} AND WORKING_CODE NOT LIKE '%[^0-9]%'
            ORDER BY CAST(WORKING_CODE AS int) ASC, DRUG_NAME COLLATE Thai_CI_AS ASC
            """))).ToList();
        var items = await repo.GetStatusAsync(null);
        Skip.If(legacy.Count < 10, "fewer than 10 active items");

        var picks = new[] { 0, 1, 2, legacy.Count / 4, legacy.Count / 3, legacy.Count / 2, (legacy.Count * 2) / 3, legacy.Count - 3, legacy.Count - 2, legacy.Count - 1 };
        foreach (var i in picks)
        {
            var l = legacy[i];
            var n = items[i];
            output.WriteLine($"A5 #{i}: {l.WORKING_CODE} {l.DRUG_NAME} qty={l.QTY_ON_HAND} rate={l.RATE_PER_MONTH} months={n.MonthsOfStockDisplay}");
            Assert.Equal(l.WORKING_CODE, n.WorkingCode);
            Assert.Equal(l.DRUG_NAME, n.DrugName);
            Assert.Equal(l.HOSP_CODE, n.HospCode);
            Assert.Equal(l.VEN, n.Ven);
            Assert.Equal(l.QTY_ON_HAND ?? 0m, n.QtyOnHand);
            Assert.Equal(l.SALE_UNIT, n.SaleUnit);
            Assert.Equal(l.LOCATION, n.Location);
            Assert.Equal(l.RATE_PER_MONTH, n.RatePerMonth);
            Assert.Equal(LegacyMonths(l.QTY_ON_HAND, l.RATE_PER_MONTH), n.MonthsOfStockDisplay);
        }
    }

    /// <summary>A6: independent re-statement of the VBScript rule (round(Q/A,2), "N/A" when A = 0).</summary>
    private static string LegacyMonths(decimal? q, decimal? a)
    {
        var rate = a ?? 0m;
        if (rate == 0m) return "N/A";
        return Math.Round((q ?? 0m) / rate, 2, MidpointRounding.ToEven).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    }

    [SkippableFact]
    public async Task A7_borrowable_is_zero_on_production_and_rule_holds_on_synthetic_rows()
    {
        var connections = db.RequireOrSkip();
        var repo = new InventoryRepository(connections);

        // Production parity: legacy per-item rule evaluated for every active item in one raw statement.
        await using var c = await connections.OpenAsync();
        var openClaims = await c.ExecuteScalarAsync<int>(ReadOnlySql.Ensure(
            "SELECT COUNT(*) FROM dbo.VCAR WHERE BORROW_QTY <> 0 AND CLOSE_STATUS <> 'C'"));
        var items = await repo.GetStatusAsync(null);
        output.WriteLine($"A7 open VCAR claims: {openClaims}; items with borrowable <> 0: {items.Count(i => i.BorrowableQty != 0)}");
        if (openClaims == 0)
        {
            Assert.All(items, i => Assert.Equal(0m, i.BorrowableQty));
        }

        // Query-shape check with table-value constructors (read-only, nothing written):
        //  item A: claim #1 of 10, borrowed 3 → 7; claim #2 of 5 (closed) → ignored; claim #3 of 4, nothing borrowed → 4 → total 11
        //  item B: claim #4 of 8, CLOSE_STATUS NULL → excluded by legacy predicate → no row → NULL
        //  item C: claim #5 BORROW_QTY 0 → excluded
        var vcar = "(VALUES (1, N'A', 10, 'O'), (2, N'A', 5, 'C'), (3, N'A', 4, 'O'), (4, N'B', 8, NULL), (5, N'C', 0, 'O')) AS v(RECORD_NUMBER, WORKING_CODE, BORROW_QTY, CLOSE_STATUS)";
        var borrow = "(VALUES (1, N'A', 2), (1, N'A', 1), (2, N'A', 5), (9, N'A', 100)) AS u(VCAR_CODE, WORKING_CODE, BORROW_QTY)";
        var joinMethod = typeof(InventoryRepository).Assembly.GetType("Invc.Infrastructure.Inventory.InventorySql")!
            .GetMethod("BorrowableJoin", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var join = (string)joinMethod.Invoke(null, [vcar, borrow])!;
        var sql = ReadOnlySql.Ensure(
            "SELECT m.WORKING_CODE AS Code, bo.BORROWABLE AS Borrowable FROM (VALUES (N'A'), (N'B'), (N'C')) AS m(WORKING_CODE) " + join + " ORDER BY m.WORKING_CODE");
        var rows = (await c.QueryAsync<(string Code, int? Borrowable)>(sql)).ToList();

        Assert.Equal(new (string, int?)[] { ("A", 11), ("B", null), ("C", null) }, rows);
    }

    [SkippableFact]
    public async Task A8_search_parity_including_thai_and_literal_wildcards()
    {
        var connections = db.RequireOrSkip();
        var repo = new InventoryRepository(connections);
        await using var c = await connections.OpenAsync();

        foreach (var keyword in new[] { "10", "ยา", "mg", "  tab  ", "50%" })
        {
            var normalized = SearchKeyword.Normalize(keyword)!;
            // Legacy LIKE filter, but with the new intentional semantics: metacharacters are literal.
            var legacyCount = await c.ExecuteScalarAsync<int>(ReadOnlySql.Ensure($"""
                SELECT COUNT(*) FROM dbo.INV_MD {ActiveFilter} AND (
                     [drug_name] LIKE @P OR [composition] LIKE @P OR [HOSP_CODE] LIKE @P OR [WORKING_CODE] LIKE @P)
                """), new { P = InventoryRepository.LikePattern(normalized) });
            var items = await repo.GetStatusAsync(keyword);
            output.WriteLine($"A8 '{keyword}' → legacy-shape {legacyCount}, new {items.Count}");
            Assert.Equal(legacyCount, items.Count);
        }

        // Legacy raw '%' would have matched everything; the new literal search must not.
        var literalPercent = await repo.GetStatusAsync("%");
        var all = await repo.GetStatusAsync(null);
        Assert.True(literalPercent.Count < all.Count || all.Count == 0, "'%' must be treated literally");
    }

    [SkippableFact]
    public async Task A9_sort_order_equals_legacy_cast_at_boundaries_and_middle()
    {
        var connections = db.RequireOrSkip();
        var repo = new InventoryRepository(connections);
        await using var c = await connections.OpenAsync();

        var legacy = (await c.QueryAsync<string>(ReadOnlySql.Ensure($"""
            SELECT WORKING_CODE FROM dbo.INV_MD {ActiveFilter} AND WORKING_CODE NOT LIKE '%[^0-9]%'
            ORDER BY CAST(WORKING_CODE AS int) ASC, DRUG_NAME COLLATE Thai_CI_AS ASC
            """))).ToList();
        var nonNumeric = await c.ExecuteScalarAsync<int>(ReadOnlySql.Ensure(
            $"SELECT COUNT(*) FROM dbo.INV_MD {ActiveFilter} AND WORKING_CODE LIKE '%[^0-9]%'"));

        var items = await repo.GetStatusAsync(null);
        var numericItems = items.Where(i => i.WorkingCode.All(char.IsDigit)).Select(i => i.WorkingCode).ToList();

        output.WriteLine($"A9 first: {string.Join(",", legacy.Take(3))} | mid: {legacy[legacy.Count / 2]} | last: {string.Join(",", legacy.TakeLast(3))}; non-numeric codes: {nonNumeric}");
        Assert.Equal(legacy, numericItems);                       // full sequence, covers first/middle/last
        Assert.Equal(legacy.Take(3), numericItems.Take(3));
        Assert.Equal(legacy.TakeLast(3), numericItems.TakeLast(3));
        // Non-numeric codes (none today) must sort after all numeric ones, never crash the query.
        var firstNonNumeric = items.ToList().FindIndex(i => !i.WorkingCode.All(char.IsDigit));
        Assert.True(firstNonNumeric == -1 || firstNonNumeric == numericItems.Count);
    }

    [SkippableFact]
    public async Task A10_lot_reconciliation_matches_raw_sql_and_reports_consistency()
    {
        var connections = db.RequireOrSkip();
        var repo = new InventoryRepository(connections);
        await using var c = await connections.OpenAsync();

        var raw = (await c.QueryAsync<(string Code, decimal Qty, decimal Value, int Lots, decimal LotQty, decimal LotValue)>(ReadOnlySql.Ensure($"""
            SELECT m.WORKING_CODE, ISNULL(m.QTY_ON_HAND,0), ISNULL(m.TOTAL_VALUE,0),
                   (SELECT COUNT(*) FROM dbo.INV_MD_C c WHERE c.WORKING_CODE = m.WORKING_CODE),
                   (SELECT ISNULL(SUM(c.QTY_ON_HAND),0) FROM dbo.INV_MD_C c WHERE c.WORKING_CODE = m.WORKING_CODE),
                   (SELECT ISNULL(SUM(c.LOT_VALUE),0) FROM dbo.INV_MD_C c WHERE c.WORKING_CODE = m.WORKING_CODE)
            FROM dbo.INV_MD m {ActiveFilter} ORDER BY m.WORKING_CODE
            """))).ToList();
        var totals = await repo.GetLotTotalsAsync();

        Assert.Equal(raw.Count, totals.Count);
        foreach (var (r, t) in raw.Zip(totals))
        {
            Assert.Equal(r.Code, t.WorkingCode);
            Assert.Equal(r.Lots, t.LotCount);
            Assert.Equal(r.LotQty, t.LotQtySum);
            Assert.Equal(r.LotValue, t.LotValueSum);
            Assert.Equal(r.Qty, t.HeaderQty);
            Assert.Equal(r.Value, t.HeaderValue);
        }

        var withLots = totals.Where(t => t.LotCount > 0).ToList();
        var qtyMismatch = withLots.Where(t => !t.QtyMatches).ToList();
        var valueMismatch = withLots.Where(t => !t.ValueMatches).ToList();
        output.WriteLine($"{DateTime.Now:O} A10 active items {totals.Count}, with lots {withLots.Count}, lots {totals.Sum(t => t.LotCount)}, " +
                         $"qty mismatches {qtyMismatch.Count}, value mismatches {valueMismatch.Count}");
        foreach (var m in qtyMismatch.Concat(valueMismatch).DistinctBy(t => t.WorkingCode).Take(10))
        {
            output.WriteLine($"  {m.WorkingCode}: header {m.HeaderQty}/{m.HeaderValue} lots {m.LotQtySum}/{m.LotValueSum}");
        }

        // Phase 0 observed full equality; assert it for items that have lots so a future divergence is visible.
        Assert.Empty(qtyMismatch);
        Assert.Empty(valueMismatch);
    }

    [SkippableFact]
    public async Task Detail_page_data_matches_status_row_and_lot_totals()
    {
        var connections = db.RequireOrSkip();
        var repo = new InventoryRepository(connections);

        var items = await repo.GetStatusAsync(null);
        Skip.If(items.Count == 0, "no active items");
        var totals = (await repo.GetLotTotalsAsync()).ToDictionary(t => t.WorkingCode);
        var sample = items.Where(i => totals[i.WorkingCode].LotCount > 0).Take(3).Concat(items.Where(i => totals[i.WorkingCode].LotCount == 0).Take(1));

        foreach (var row in sample)
        {
            var d = await repo.GetDetailAsync(row.WorkingCode);
            Assert.NotNull(d);
            Assert.Equal(row.DrugName, d!.DrugName);
            Assert.Equal(row.QtyOnHand, d.QtyOnHand);
            Assert.Equal(row.RatePerMonth, d.RatePerMonth);
            Assert.Equal(row.MonthsOfStockDisplay, d.MonthsOfStockDisplay);
            Assert.Equal(totals[row.WorkingCode].LotCount, d.Lots.Count);           // no row multiplication from DRUG_VN/COMPANY
            Assert.Equal(totals[row.WorkingCode].LotQtySum, d.Reconciliation.LotQtySum);
            Assert.Equal(totals[row.WorkingCode].LotValueSum, d.Reconciliation.LotValueSum);
            Assert.False(d.IsInactive);
        }

        Assert.Null(await repo.GetDetailAsync("ZZZZZZZ"));
        Assert.Null(await repo.GetDetailAsync("1'; --"));
    }

    private sealed record LegacyRow(string WORKING_CODE, string DRUG_NAME, string? HOSP_CODE, string? VEN,
        decimal? QTY_ON_HAND, string? SALE_UNIT, string? LOCATION, decimal? RATE_PER_MONTH);
}
