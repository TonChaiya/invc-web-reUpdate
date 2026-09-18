using Dapper;
using Invc.Core.Dashboard;
using Invc.Core.PurchaseOrders;
using Invc.Core.Receipts;
using Invc.Core.Reorder;
using Invc.Infrastructure.Dashboard;
using Invc.Infrastructure.Data;
using Invc.Infrastructure.Inventory;
using Invc.Infrastructure.PurchaseOrders;
using Invc.Infrastructure.Receipts;
using Invc.Infrastructure.Reorder;
using Xunit.Abstractions;

namespace Invc.IntegrationTests;

/// <summary>
/// Phase 6 parity D1–D10, C11 and cross-domain consistency — read-only against live INV.
/// Raw side is the legacy default.asp / Dashboard.asp / table_*.asp SQL (docs/legacy-query-map.md).
/// </summary>
public class DashboardParityTests(ProductionReadOnlyFixture db, ITestOutputHelper output) : IClassFixture<ProductionReadOnlyFixture>
{
    private async Task<(DashboardSnapshot snap, DashboardService svc)> BuildAsync(string? item = null)
    {
        var c = db.RequireOrSkip();
        var svc = new DashboardService(new InventoryRepository(c), new ReorderRepository(c), new PurchaseOrderRepository(c),
                                       new NonPoReceiptRepository(c), new DashboardAnalyticsRepository(c));
        var snap = await svc.BuildAsync(null, item, DateTime.Now);
        output.WriteLine($"{DateTime.Now:O} dashboard FY {snap.FiscalYear} (note: {snap.FiscalYearNote ?? "-"}), failed sections {snap.FailedSectionCount}");
        foreach (var (name, ok, err) in new (string, bool, string?)[]
        {
            ("Inventory", snap.Inventory.IsAvailable, snap.Inventory.Error), ("Reorder", snap.Reorder.IsAvailable, snap.Reorder.Error),
            ("PurchaseOrders", snap.PurchaseOrders.IsAvailable, snap.PurchaseOrders.Error), ("Receipts", snap.Receipts.IsAvailable, snap.Receipts.Error),
            ("Budget", snap.Budget.IsAvailable, snap.Budget.Error), ("Substock", snap.Substock.IsAvailable, snap.Substock.Error),
            ("Coverage", snap.Coverage.IsAvailable, snap.Coverage.Error), ("EdNed", snap.EdNed.IsAvailable, snap.EdNed.Error),
            ("Agreements", snap.Agreements.IsAvailable, snap.Agreements.Error), ("Movement", snap.Movement.IsAvailable, snap.Movement.Error),
            ("ProcessTime", snap.ProcessTime.IsAvailable, snap.ProcessTime.Error), ("ItemTrend", snap.ItemTrend.IsAvailable, snap.ItemTrend.Error),
        })
        {
            if (!ok) { output.WriteLine($"  section {name} unavailable: {err}"); }
        }
        Assert.Equal(0, snap.FailedSectionCount);
        return (snap, svc);
    }

    [SkippableFact]
    public async Task D1_budget_matches_legacy_sum_for_open_year()
    {
        var (snap, _) = await BuildAsync();
        await using var c = await db.RequireOrSkip().OpenAsync();
        var raw = await c.QuerySingleOrDefaultAsync<(string? Year, decimal? Sum)>(ReadOnlySql.Ensure(
            "SELECT BUDGET.[year], SUM(BUDGET.money) FROM dbo.BUDGET WHERE BUDGET.BudgetOpen = 'O' GROUP BY [year]"));
        var rawSelected = await c.QuerySingleAsync<(int Rows, decimal? Sum)>(ReadOnlySql.Ensure(
            "SELECT COUNT(*), SUM(money) FROM dbo.BUDGET WHERE [year] = @Y"), new { Y = snap.FiscalYear.ToString() });

        var b = snap.Budget.Value!;
        output.WriteLine($"D1 open year raw {raw.Year} / {raw.Sum}; selected FY{snap.FiscalYear} raw rows {rawSelected.Rows} sum {rawSelected.Sum}; new {b.RowCount} / {b.TotalMoney} (HasBudget {b.HasBudget})");
        Assert.Equal(rawSelected.Rows, b.RowCount);
        Assert.Equal(rawSelected.Sum, b.TotalMoney);
        if (raw.Year is not null && int.TryParse(raw.Year, out var openYear) && snap.FiscalYearNote is null)
        {
            Assert.Equal(openYear, snap.FiscalYear);      // default year = the single open budget year
            Assert.Equal(raw.Sum, b.TotalMoney);
        }
    }

    [SkippableFact]
    public async Task D2_purchase_order_cards_match_legacy_buckets_and_phase4_report()
    {
        var (snap, _) = await BuildAsync();
        var c0 = db.RequireOrSkip();
        await using var c = await c0.OpenAsync();
        var prefix = PurchaseOrderRules.PoPrefix(snap.FiscalYear);
        var sets = new (PurchaseOrderBucket Bucket, string Where)[]
        {
            (PurchaseOrderBucket.Issued, "STATUS NOT IN ('0','C')"),
            (PurchaseOrderBucket.Received, "STATUS IN ('2','3','4','5','6','7','8','9','D')"),
            (PurchaseOrderBucket.Accounting, "STATUS IN ('4','5','6','7','8','9','D')"),
            (PurchaseOrderBucket.Finance, "STATUS IN ('4','5','7','8','9')"),
            (PurchaseOrderBucket.Closed, "STATUS IN ('5','9')"),
        };
        var independent = PurchaseOrderReport.Build(snap.FiscalYear, await new PurchaseOrderRepository(c0).GetHeadersAsync(snap.FiscalYear, null), PurchaseOrderBucket.Issued);
        foreach (var (bucket, where) in sets)
        {
            var raw = await c.QuerySingleAsync<(int N, decimal? V)>(ReadOnlySql.Ensure(
                $"SELECT COUNT(MS_PO.PO_NO), SUM([TOTAL_COST]) FROM dbo.MS_PO WHERE Left([PO_NO],2) = @P AND {where}"), new { P = prefix });
            var card = snap.PurchaseOrders.Value!.Buckets[bucket];
            output.WriteLine($"D2 {bucket}: legacy {raw.N} / {raw.V ?? 0m}; dashboard {card.Count} / {card.TotalValue}; phase4 {independent.Buckets[bucket].Count} / {independent.Buckets[bucket].TotalValue}");
            Assert.Equal(raw.N, card.Count);
            Assert.Equal(raw.V ?? 0m, card.TotalValue);
            Assert.Equal(independent.Buckets[bucket].Count, card.Count);
            Assert.Equal(independent.Buckets[bucket].TotalValue, card.TotalValue);
        }
    }

    [SkippableFact]
    public async Task D3_D8_main_store_value_and_item_count_definitions()
    {
        var (snap, _) = await BuildAsync();
        var c0 = db.RequireOrSkip();
        await using var c = await c0.OpenAsync();
        var allRows = await c.ExecuteScalarAsync<decimal?>(ReadOnlySql.Ensure("SELECT Sum(INV_MD.TOTAL_VALUE) FROM dbo.INV_MD"));                     // Dashboard.asp
        var active = await c.ExecuteScalarAsync<decimal?>(ReadOnlySql.Ensure("SELECT Sum(INV_MD.TOTAL_VALUE) FROM dbo.INV_MD WHERE NOUSE IS NULL"));  // Phase 2 definition
        var byEd = await c.ExecuteScalarAsync<decimal?>(ReadOnlySql.Ensure(
            "SELECT Sum(INV_MD.TOTAL_VALUE) FROM dbo.INV_MD INNER JOIN dbo.TBLED_NED ON INV_MD.ED_NED = TBLED_NED.EDCODE"));                          // default.asp Drug
        var count = await c.ExecuteScalarAsync<int>(ReadOnlySql.Ensure("SELECT count(WORKING_CODE) FROM dbo.INV_MD WHERE NoUse IS NULL"));         // D8
        var inventorySummary = await new InventoryRepository(c0).GetSummaryAsync();

        var inv = snap.Inventory.Value!;
        output.WriteLine($"D3 legacy all-rows {allRows}, default.asp ED-join {byEd}, active-only {active}; dashboard {inv.TotalValue} (Phase 2 active definition)");
        output.WriteLine($"D8 raw active count {count}; dashboard {inv.ActiveItemCount}; inventory summary {inventorySummary.ActiveItemCount}");
        Assert.Equal(active ?? 0m, inv.TotalValue);
        Assert.Equal(inventorySummary.TotalValue, inv.TotalValue);
        Assert.Equal(count, inv.ActiveItemCount);
        Assert.Equal(inventorySummary.ActiveItemCount, inv.ActiveItemCount);
        if (allRows != active) { output.WriteLine($"D3 NOTE: all-rows and active definitions differ by {(allRows ?? 0m) - (active ?? 0m)} (inactive items carry value)"); }
    }

    [SkippableFact]
    public async Task D4_substock_total_and_department_breakdown()
    {
        var (snap, _) = await BuildAsync();
        await using var c = await db.RequireOrSkip().OpenAsync();
        var total = await c.ExecuteScalarAsync<decimal?>(ReadOnlySql.Ensure("SELECT Sum(Substock.TOTAL_VALUE) FROM dbo.Substock"));
        var byDept = (await c.QueryAsync<(string Dept, decimal? V)>(ReadOnlySql.Ensure(
            "SELECT DEPT_ID.DEPT_NAME, Sum(TOTAL_VALUE) FROM dbo.SUBSTOCK, dbo.DEPT_ID WHERE DEPT_ID.DEPT_ID = SUBSTOCK.DEPT_ID AND TOTAL_VALUE is not null GROUP BY DEPT_NAME"))).ToList();
        var rows = await c.ExecuteScalarAsync<int>(ReadOnlySql.Ensure("SELECT COUNT(*) FROM dbo.SUBSTOCK"));

        var sub = snap.Substock.Value!;
        output.WriteLine($"D4 raw total {total} rows {rows}; dashboard {sub.TotalValue} rows {sub.RowCount}; depts raw {byDept.Count} new {sub.ByDepartment.Count}");
        Assert.Equal(total ?? 0m, sub.TotalValue);
        Assert.Equal(rows, sub.RowCount);
        Assert.Equal(sub.TotalValue, sub.ByDepartment.Sum(d => d.TotalValue));
        foreach (var d in byDept) { Assert.Equal(d.V ?? 0m, sub.ByDepartment.Single(x => x.DeptName == d.Dept).TotalValue); }
    }

    [SkippableFact]
    public async Task D5_stock_coverage_matches_legacy_kpi()
    {
        var (snap, _) = await BuildAsync();
        await using var c = await db.RequireOrSkip().OpenAsync();
        var latest = await c.QuerySingleOrDefaultAsync<(string Y, string M, decimal? Sale)>(ReadOnlySql.Ensure(
            "SELECT TOP 1 MBS_RE_M.YEAR, MBS_RE_M.MONTH, Sum(MBS_RE_M.SALE_VALUE) FROM dbo.MBS_RE_M GROUP BY MBS_RE_M.YEAR, MBS_RE_M.MONTH ORDER BY MBS_RE_M.YEAR DESC, MBS_RE_M.MONTH DESC"));
        var cov = snap.Coverage.Value!;
        if (latest.Y is null)
        {
            Assert.False(cov.HasPeriod);
            output.WriteLine("D5 no MBS_RE_M period; dashboard shows N/A");
            return;
        }
        var mnth = await c.ExecuteScalarAsync<decimal?>(ReadOnlySql.Ensure(
            "SELECT SUM(MNTH_SUM.TOTAL_VALUE) FROM dbo.MNTH_SUM WHERE MNTH_SUM.YEAR = @Y AND MNTH_SUM.MONTH = @M"), new { latest.Y, latest.M });
        var legacyRatio = latest.Sale is > 0m && mnth.HasValue ? mnth.Value / latest.Sale.Value : (decimal?)null;

        output.WriteLine($"D5 period {latest.Y}-{latest.M}: MNTH_SUM {mnth}, SALE {latest.Sale}, legacy ratio {legacyRatio}; dashboard {cov.Year}-{cov.Month} {cov.MonthEndValue} / {cov.SaleValue} = {cov.Ratio} ({cov.DisplayPeriod})");
        Assert.Equal(latest.Y, cov.Year);
        Assert.Equal(latest.M, cov.Month);
        Assert.Equal(mnth, cov.MonthEndValue);
        Assert.Equal(latest.Sale, cov.SaleValue);
        Assert.Equal(legacyRatio, cov.Ratio);
    }

    [SkippableFact]
    public async Task D6_ed_ned_legacy_filter_vs_phase2_summary()
    {
        var (snap, _) = await BuildAsync();
        await using var c = await db.RequireOrSkip().OpenAsync();
        var raw = (await c.QueryAsync<(string? Code, int N, decimal? V)>(ReadOnlySql.Ensure(
            "SELECT INV_MD.ED_NED, Count(INV_MD.WORKING_CODE), Sum(INV_MD.TOTAL_VALUE) FROM dbo.INV_MD WHERE INV_MD.NOUSE Is Null AND INV_MD.OUT_OF_LIST Is Null AND INV_MD.PO_INDIVIDUAL Is Null GROUP BY INV_MD.ED_NED ORDER BY INV_MD.ED_NED"))).ToList();
        var ed = snap.EdNed.Value!;
        var phase2 = snap.Inventory.Value!.Groups;

        Assert.Equal(raw.Count, ed.Count);
        foreach (var (r, n) in raw.Zip(ed))
        {
            output.WriteLine($"D6 {r.Code}: legacy {r.N} / {r.V}; dashboard {n.ItemCount} / {n.TotalValue} ({n.ShortName}); phase2 group {phase2.FirstOrDefault(g => g.EdNedCode == r.Code)?.ItemCount}");
            Assert.Equal(r.Code, n.Code);
            Assert.Equal(r.N, n.ItemCount);
            Assert.Equal(r.V ?? 0m, n.TotalValue);
        }
        var same = raw.All(r => phase2.FirstOrDefault(g => g.EdNedCode == r.Code)?.ItemCount == r.N) && phase2.Count == raw.Count;
        output.WriteLine($"D6 legacy filter == Phase 2 (NOUSE only) filter today: {same}");
    }

    [SkippableFact]
    public async Task D7_agreements_active_count_and_remaining_value()
    {
        var rows = await new DashboardAnalyticsRepository(db.RequireOrSkip()).GetActiveAgreementsAsync(DateTime.Now);   // surfaces repository errors directly
        output.WriteLine($"D7 raw active rows via repository: {rows.Count}");
        var (snap, _) = await BuildAsync();
        await using var c = await db.RequireOrSkip().OpenAsync();
        var raw = await c.QuerySingleAsync<(int N, decimal? Remaining)>(ReadOnlySql.Ensure("""
            SELECT COUNT(*), sum((AgreeQty/PACK_RATIO)*UnitPrice) - sum((BuyQty/PACK_RATIO)*UnitPrice)
            FROM dbo.Agreement WHERE BuyQty < AgreeQty AND Expdate > getdate() AND PACK_RATIO > 0
            """));
        var ag = snap.Agreements.Value!;
        output.WriteLine($"D7 raw active {raw.N} remaining {raw.Remaining}; dashboard active {ag.ActiveCount} remaining {ag.RemainingValue} unvaluable {ag.UnvaluableCount} (HasActive {ag.HasActiveAgreements})");
        Assert.Equal(raw.N + ag.UnvaluableCount, ag.ActiveCount);
        Assert.Equal(raw.Remaining ?? 0m, ag.RemainingValue);
        if (raw.N == 0) { Assert.False(ag.HasActiveAgreements); }
    }

    [SkippableFact]
    public async Task D9_monthly_movement_categories_and_values_match_legacy_grouping()
    {
        var (snap, _) = await BuildAsync();
        await using var c = await db.RequireOrSkip().OpenAsync();
        // table_monthly_rpt.asp: MType = R_S_STATUS + LEFT(R_S_NUMBER,1), Mnth = yyyymm + 54300, for the Thai fiscal year.
        var raw = (await c.QueryAsync<(string MType, string Mnth, int N, decimal? V)>(ReadOnlySql.Ensure("""
            SELECT R_S_STATUS + LEFT(R_S_NUMBER,1), CAST(CAST(LEFT(CONVERT(char(8), OPERATE_DATE, 112), 6) AS int) + 54300 AS varchar(6)), COUNT(*), SUM(card.[VALUE])
            FROM dbo.CARD
            WHERE CASE WHEN MONTH(OPERATE_DATE) >= 10 THEN YEAR(OPERATE_DATE) + 543 + 1 ELSE YEAR(OPERATE_DATE) + 543 END = @Fy
            GROUP BY R_S_STATUS + LEFT(R_S_NUMBER,1), LEFT(CONVERT(char(8), OPERATE_DATE, 112), 6)
            """), new { Fy = snap.FiscalYear })).ToList();
        var months = snap.Movement.Value!;
        var flat = months.SelectMany(m => m.Categories.Select(cat => (m.MonthKey, cat.Category.Key, cat.Count, cat.Value))).ToList();

        output.WriteLine($"D9 FY{snap.FiscalYear}: raw cells {raw.Count} (categories {string.Join(",", raw.Select(r => r.MType).Distinct().Order())}); dashboard cells {flat.Count}, months {months.Count}");
        Assert.Equal(raw.Count, flat.Count);
        foreach (var r in raw)
        {
            var n = flat.Single(f => f.MonthKey == r.Mnth && f.Key == r.MType);
            Assert.Equal(r.N, n.Count);
            Assert.Equal(r.V ?? 0m, n.Value);
        }
        // Simplified receive/issue/other must partition every category (nothing dropped).
        foreach (var m in months) { Assert.Equal(m.Categories.Sum(cat => cat.Value), m.ReceiveValue + m.IssueValue + m.OtherValue); }
    }

    [SkippableFact]
    public async Task D10_item_trend_matches_legacy_table_by_item_for_representative_items()
    {
        var c0 = db.RequireOrSkip();
        await using var c = await c0.OpenAsync();
        var items = (await c.QueryAsync<string>(ReadOnlySql.Ensure(
            "SELECT TOP 2 WORKING_CODE FROM dbo.CARD WHERE R_S_STATUS = 'S' GROUP BY WORKING_CODE ORDER BY COUNT(*) DESC, WORKING_CODE"))).ToList();
        Skip.If(items.Count == 0, "no issue rows in CARD");
        var repo = new DashboardAnalyticsRepository(c0);
        var from = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-(DashboardService.TrendMonths - 1));

        foreach (var code in items)
        {
            var raw = (await c.QueryAsync<(string DMonth, decimal? Q, decimal? V)>(ReadOnlySql.Ensure("""
                SELECT CAST(CAST(left(convert(CHAR(8),OPERATE_DATE,112),6) AS int) + 54300 AS varchar(6)), sum(ACTIVE_QTY1+ACTIVE_QTY2+ACTIVE_QTY3), sum(c.[VALUE])
                FROM dbo.CARD c WHERE c.WORKING_CODE = @Code AND R_S_STATUS = 'S' AND OPERATE_DATE >= @From
                GROUP BY left(convert(CHAR(8),OPERATE_DATE,112),6) ORDER BY 1 DESC
                """), new { Code = code, From = from })).ToList();
            var trend = await repo.GetItemTrendAsync(code, DashboardService.TrendMonths);
            Assert.NotNull(trend);
            output.WriteLine($"D10 {code} {trend!.DrugName}: raw months {raw.Count}, new {trend.Months.Count}");
            Assert.Equal(raw.Select(r => r.DMonth), trend.Months.Select(m => m.MonthKey));
            foreach (var (r, n) in raw.Zip(trend.Months))
            {
                Assert.Equal(r.Q ?? 0m, n.SaleQuantity);
                Assert.Equal(r.V ?? 0m, n.SaleValue);
            }
        }
        Assert.Null(await repo.GetItemTrendAsync("ZZZZZZZ", 12));
        Assert.Null(await repo.GetItemTrendAsync("2010930'", 12));
    }

    [SkippableFact]
    public async Task C11_process_time_measurement_matches_legacy_and_reports_na_when_empty()
    {
        var (snap, _) = await BuildAsync();
        await using var c = await db.RequireOrSkip().OpenAsync();
        var raw = (await c.QueryAsync<(string PoMonth, decimal? Send, decimal? Doc, decimal? Acc)>(ReadOnlySql.Ensure("""
            SELECT LEFT(CONVERT(varchar, PO_date,112),6), avg(CAST(datediff(d,[PO_DATE],[BILLIN]) AS decimal(10,2))), avg(CAST(datediff(d,[BILLIN],[BillOUT]) AS decimal(10,2))), avg(CAST(datediff(d,[BILLOUT],[BillEND]) AS decimal(10,2)))
            FROM dbo.MS_PO WHERE LEFT(PO_NO,2) = @P GROUP BY LEFT(CONVERT(varchar, PO_date,112),6) ORDER BY 1 DESC
            """), new { P = PurchaseOrderRules.PoPrefix(snap.FiscalYear) })).ToList();
        var pt = snap.ProcessTime.Value!;
        output.WriteLine($"C11 FY{snap.FiscalYear}: months {raw.Count}; with any completed stage {pt.Count(r => r.HasAnyValue)}");
        Assert.Equal(raw.Count, pt.Count);
        foreach (var (r, n) in raw.Zip(pt))
        {
            output.WriteLine($"  {r.PoMonth}: send {r.Send?.ToString() ?? "NULL"} doc {r.Doc?.ToString() ?? "NULL"} acc {r.Acc?.ToString() ?? "NULL"}");
            Assert.Equal(r.PoMonth, n.PoMonth);
            Assert.Equal(r.Send, n.SendDays);
            Assert.Equal(r.Doc, n.DocDays);
            Assert.Equal(r.Acc, n.AccDays);
        }
    }

    [SkippableFact]
    public async Task Cross_domain_dashboard_equals_trusted_module_reports()
    {
        var (snap, _) = await BuildAsync();
        var c0 = db.RequireOrSkip();
        var inv = await new InventoryRepository(c0).GetSummaryAsync();
        var reorder = ReorderReport.Build(await new ReorderRepository(c0).GetEligibleAsync(null), ReorderStatusFilter.All);
        var po = PurchaseOrderReport.Build(snap.FiscalYear, await new PurchaseOrderRepository(c0).GetHeadersAsync(snap.FiscalYear, null), PurchaseOrderBucket.Issued);
        var rc = ReceiptReport.Build(snap.FiscalYear, await new NonPoReceiptRepository(c0).GetHeadersAsync(snap.FiscalYear, null), null, null);

        output.WriteLine($"cross-domain: inventory {inv.ActiveItemCount}/{inv.TotalValue}; reorder {reorder.RedCount}/{reorder.YellowCount}/{reorder.RedSuggestedTotal}; PO issued {po.Buckets[PurchaseOrderBucket.Issued].Count}; receipts {rc.HeaderCount}/{rc.LineCount}/{rc.TotalValue}");
        Assert.Equal((inv.ActiveItemCount, inv.TotalQtyOnHand, inv.TotalValue), (snap.Inventory.Value!.ActiveItemCount, snap.Inventory.Value.TotalQtyOnHand, snap.Inventory.Value.TotalValue));
        Assert.Equal((reorder.RedCount, reorder.YellowCount, reorder.GreenCount, reorder.RedSuggestedTotal),
                     (snap.Reorder.Value!.RedCount, snap.Reorder.Value.YellowCount, snap.Reorder.Value.GreenCount, snap.Reorder.Value.RedSuggestedTotal));
        foreach (var b in Enum.GetValues<PurchaseOrderBucket>()) { Assert.Equal((po.Buckets[b].Count, po.Buckets[b].TotalValue), (snap.PurchaseOrders.Value!.Buckets[b].Count, snap.PurchaseOrders.Value.Buckets[b].TotalValue)); }
        Assert.Equal((rc.HeaderCount, rc.LineCount, rc.TotalValue, rc.TypeCount), (snap.Receipts.Value!.HeaderCount, snap.Receipts.Value.LineCount, snap.Receipts.Value.TotalValue, snap.Receipts.Value.TypeCount));
    }

    [SkippableFact]
    public async Task Dashboard_isolates_optional_section_failure()
    {
        var c0 = db.RequireOrSkip();
        var svc = new DashboardService(new InventoryRepository(c0), new ReorderRepository(c0), new PurchaseOrderRepository(c0),
                                       new NonPoReceiptRepository(c0), new ThrowingAnalytics());
        var snap = await svc.BuildAsync(2569, null, DateTime.Now);
        Assert.True(snap.Inventory.IsAvailable);
        Assert.True(snap.PurchaseOrders.IsAvailable);
        Assert.False(snap.Budget.IsAvailable);
        Assert.False(snap.Movement.IsAvailable);
        Assert.Equal("InvalidOperationException", snap.Budget.Error);
        Assert.DoesNotContain("Server=", snap.Budget.Error ?? string.Empty);
    }

    private sealed class ThrowingAnalytics : IDashboardAnalyticsRepository
    {
        private static Exception Boom() => new InvalidOperationException("Server=secret;Password=secret");
        public Task<DashboardBudget> GetBudgetAsync(int fiscalYear, CancellationToken ct = default) => throw Boom();
        public Task<DashboardSubstock> GetSubstockAsync(CancellationToken ct = default) => throw Boom();
        public Task<DashboardStockCoverage> GetStockCoverageAsync(CancellationToken ct = default) => throw Boom();
        public Task<IReadOnlyList<DashboardEdNedRow>> GetLegacyEdNedAsync(CancellationToken ct = default) => throw Boom();
        public Task<IReadOnlyList<DashboardAgreementRow>> GetActiveAgreementsAsync(DateTime today, CancellationToken ct = default) => throw Boom();
        public Task<IReadOnlyList<DashboardMovementRow>> GetMovementAsync(int fiscalYear, CancellationToken ct = default) => throw Boom();
        public Task<IReadOnlyList<DashboardMovementItemTypeRow>> GetMovementByItemTypeAsync(int fiscalYear, CancellationToken ct = default) => throw Boom();
        public Task<IReadOnlyList<DashboardProcessTimeRow>> GetProcessTimeAsync(int fiscalYear, CancellationToken ct = default) => throw Boom();
        public Task<DashboardItemTrend?> GetItemTrendAsync(string workingCode, int months, CancellationToken ct = default) => throw Boom();
    }
}
