using Invc.Core.Dashboard;
using Invc.Core.Inventory;
using Invc.Core.PurchaseOrders;
using Invc.Core.Receipts;
using Invc.Core.Reorder;

namespace Invc.UnitTests;

public class DashboardRulesTests
{
    [Theory]
    [InlineData("104897.85", "40073.42", "2.62")]
    [InlineData("100", "50", "2")]
    [InlineData("0", "50", "0")]
    public void StockCoverage_divides_month_end_value_by_sales(string mnth, string sale, string expected)
        => Assert.Equal(decimal.Parse(expected), Math.Round(DashboardRules.StockCoverage(decimal.Parse(mnth), decimal.Parse(sale))!.Value, 2));

    [Theory]
    [InlineData("100", "0")]
    [InlineData("100", null)]
    [InlineData(null, "50")]
    public void StockCoverage_is_null_on_zero_or_null_inputs(string? mnth, string? sale)
        => Assert.Null(DashboardRules.StockCoverage(mnth is null ? null : decimal.Parse(mnth), sale is null ? null : decimal.Parse(sale)));

    [Theory]
    [InlineData(1000, 400, 100, "50", "300")]     // (1000-400)/100*50
    [InlineData(1000, 1000, 100, "50", "0")]
    [InlineData(1000, 400, 0, "50", null)]        // zero pack ratio → null, never divide
    [InlineData(1000, 400, null, "50", null)]
    [InlineData(1000, null, 100, "50", "500")]    // null BuyQty read as 0 (legacy sum semantics)
    [InlineData(null, 400, 100, "50", null)]      // no AgreeQty → no value
    [InlineData(1000, 400, 100, null, null)]
    public void AgreementRemaining_guards_pack_ratio_and_nulls(int? agree, int? buy, int? pack, string? price, string? expected)
        => Assert.Equal(expected is null ? null : decimal.Parse(expected),
            DashboardRules.AgreementRemaining(agree, buy, pack, price is null ? null : decimal.Parse(price)));

    [Theory]
    [InlineData(2026, 9, 30, 2569, "256909")]
    [InlineData(2026, 10, 1, 2570, "256910")]
    [InlineData(2025, 10, 1, 2569, "256810")]
    public void Movement_month_key_and_fiscal_year_use_shared_rules(int y, int m, int d, int fy, string monthKey)
    {
        var date = new DateTime(y, m, d);
        Assert.Equal(fy, DashboardRules.FiscalYearOf(date));
        Assert.Equal(monthKey, DashboardRules.ThaiMonthKey(date));
    }

    [Theory]
    [InlineData("2569", 2569)]
    [InlineData("2026", null)]
    [InlineData("x", null)]
    [InlineData(null, null)]
    public void FiscalYear_input_validation(string? raw, int? expected)
        => Assert.Equal(expected, DashboardRules.TryParseFiscalYear(raw));

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData(" 1001710 ", "1001710")]
    [InlineData("1001710'", null)]
    [InlineData("12345678", null)]
    public void ItemCode_validation_for_trend(string? raw, string? expected)
        => Assert.Equal(expected, DashboardRules.NormalizeItemCode(raw));
}

public class DashboardCompositionTests
{
    private static DashboardProcessedSnapshotRow Snap(int y, int m, string? ed, decimal qty, decimal val, int items = 1)
        => new(y, m, ed, ed switch { "1" => "ยาในบัญชียาหลักแห่งชาติ", "2" => "ยานอกบัญชียาหลักแห่งชาติ", "3" => "วัสดุการแพทย์", "4" => "วัสดุเภสัชกรรม", "5" => "ยาตัวอย่างเพื่อทดลองใช้", _ => null }, items, qty, val);
    private static DashboardProcessedFlowRow Flow(int y, int m, string? ed, decimal rq, decimal rv, decimal sq, decimal sv)
        => new(y, m, ed, ed switch { "1" => "ยาในบัญชียาหลักแห่งชาติ", "2" => "ยานอกบัญชียาหลักแห่งชาติ", "3" => "วัสดุการแพทย์", _ => null }, 1, rq, rv, sq, sv);

    [Fact]
    public void Processed_month_keys_follow_the_thai_fiscal_year_and_ce_storage()
    {
        Assert.Equal("256810", DashboardMonthKey.FromCe(2025, 10));
        Assert.Equal(["256810", "256811", "256812", "256901", "256902", "256903", "256904", "256905", "256906", "256907", "256908", "256909"], DashboardMonthKey.FiscalYearMonths(2569));
        Assert.Equal("256809", DashboardMonthKey.Previous("256810"));
        Assert.Equal("256812", DashboardMonthKey.Previous("256901"));
        Assert.Equal("ก.ย. 2569", DashboardMonthKey.Display("256909"));
        Assert.Equal(("202509", "202609"), Invc.Infrastructure.Dashboard.DashboardAnalyticsRepository.ProcessedWindow(2569, includePrecedingMonth: true));
        Assert.Equal(("202510", "202609"), Invc.Infrastructure.Dashboard.DashboardAnalyticsRepository.ProcessedWindow(2569, includePrecedingMonth: false));
    }

    [Fact]
    public void Processed_months_carry_previous_ending_as_opening_and_satisfy_the_identity_per_type()
    {
        // Live-shaped fixture (Sep 2026 evidence): Aug ending by type → Sep opening; MBS_RE_M flows; Sep ending.
        var snaps = new[]
        {
            Snap(2026, 8, "1", 148_812, 75_537.79m, 218), Snap(2026, 8, "2", 3_516, 681.89m, 12), Snap(2026, 8, "3", 8_617, 28_678.17m, 86),
            Snap(2026, 9, "1", 142_672, 71_502.19m, 218), Snap(2026, 9, "2", 3_016, 446.89m, 12), Snap(2026, 9, "3", 8_558, 27_097.82m, 86),
        };
        var flows = new[]
        {
            Flow(2026, 9, "1", 54_000, 28_858.65m, 60_140, 32_894.25m), Flow(2026, 9, "2", 0, 0m, 500, 235m), Flow(2026, 9, "3", 111, 670m, 170, 2_250.35m),
        };
        var r = DashboardProcessedMovementBuilder.Build(2569, snaps, flows);
        Assert.Equal(["256909", "256908"], r.Months.Select(m => m.MonthKey));          // newest processed month first, only processed months
        var sep = r.Months[0];
        Assert.Equal("256908", sep.PreviousMonthKey);
        Assert.Equal(104_897.85m, sep.OpeningValue);                                  // previous MNTH_SUM ending becomes opening
        Assert.Equal(29_528.65m, sep.ReceiveValue); Assert.Equal(35_379.60m, sep.IssueValue); Assert.Equal(99_046.90m, sep.EndingValue);
        Assert.Equal(99_046.90m, sep.CalculatedEnding); Assert.Equal(0m, sep.Difference); Assert.Equal(0m, sep.QtyDifference);
        Assert.Equal(316, sep.ItemCount);
        // by type: exact identity, ED/NED separate, drug subtotal = 1 + 2 only, grand total = actual categories
        Assert.Equal(["1", "2", "3"], sep.Types.Select(t => t.Code));
        Assert.Equal((75_537.79m, 28_858.65m, 32_894.25m, 71_502.19m, 0m), (sep.Type("1")!.OpeningValue, sep.Type("1")!.ReceiveValue, sep.Type("1")!.IssueValue, sep.Type("1")!.EndingValue, sep.Type("1")!.Difference));
        Assert.Equal((681.89m, 0m, 235m, 446.89m, 0m), (sep.Type("2")!.OpeningValue, sep.Type("2")!.ReceiveValue, sep.Type("2")!.IssueValue, sep.Type("2")!.EndingValue, sep.Type("2")!.Difference));
        Assert.Equal((28_678.17m, 670m, 2_250.35m, 27_097.82m, 0m), (sep.Type("3")!.OpeningValue, sep.Type("3")!.ReceiveValue, sep.Type("3")!.IssueValue, sep.Type("3")!.EndingValue, sep.Type("3")!.Difference));
        Assert.Equal((76_219.68m, 28_858.65m, 33_129.25m, 71_949.08m), (sep.DrugOpeningValue, sep.DrugReceiveValue, sep.DrugIssueValue, sep.DrugEndingValue));
        Assert.Equal(104_897.85m, sep.TypeOpeningTotal); Assert.Equal(29_528.65m, sep.TypeReceiveTotal); Assert.Equal(35_379.60m, sep.TypeIssueTotal); Assert.Equal(99_046.90m, sep.TypeEndingTotal);
        Assert.True(sep.TypesMatchTotals);
        Assert.Equal(sep.OpeningValue, sep.TypeOpeningTotal);                         // drug subtotal not added again
        // Aug is the first snapshot loaded → no previous calendar month → opening null, never 0
        var aug = r.Months[1];
        Assert.False(aug.HasOpening); Assert.Null(aug.OpeningValue); Assert.Null(aug.Difference); Assert.Null(aug.PreviousMonthKey);
        Assert.Equal(0m, aug.ReceiveValue); Assert.Equal(104_897.85m, aug.EndingValue);
        Assert.All(aug.Types, t => Assert.Null(t.OpeningValue));
    }

    [Fact]
    public void Processed_months_do_not_bridge_missing_calendar_months_and_keep_unknown_and_other_types()
    {
        var snaps = new[]
        {
            Snap(2025, 9, "1", 10, 100m),                       // Sep 2025: preceding month of FY2569 — opening source for Oct only
            Snap(2025, 10, "1", 12, 120m), Snap(2025, 10, "4", 1, 5m), Snap(2025, 10, "5", 2, 7m), Snap(2025, 10, null, 3, 9m),
            Snap(2025, 12, "1", 20, 200m),                      // Nov 2025 NOT processed → Dec has no opening
            Snap(2026, 9, "1", 30, 300m),                       // Sep 2026 (Aug missing) → no opening
            Snap(2026, 10, "1", 99, 999m),                      // Oct 2026 belongs to FY2570 → excluded
        };
        var flows = new[] { Flow(2025, 10, "1", 5, 50m, 3, 30m), Flow(2025, 10, "4", 1, 5m, 0, 0m), Flow(2025, 10, "5", 2, 7m, 0, 0m), Flow(2025, 10, null, 3, 9m, 0, 0m), Flow(2025, 12, "1", 1, 10m, 0, 0m) };
        var r = DashboardProcessedMovementBuilder.Build(2569, snaps, flows);
        Assert.Equal(["256909", "256812", "256810"], r.Months.Select(m => m.MonthKey));   // Sep 2025 is not a FY month; Oct 2026 excluded
        var oct = r.Months.Single(m => m.MonthKey == "256810");
        Assert.Equal(100m, oct.OpeningValue); Assert.Equal("256809", oct.PreviousMonthKey);
        Assert.Equal(141m, oct.EndingValue); Assert.Equal(71m, oct.ReceiveValue); Assert.Equal(30m, oct.IssueValue);
        Assert.Equal(141m - (100m + 71m - 30m), oct.Difference);                            // fixture identity does not hold → difference reported, not hidden
        Assert.Equal(["1", "4", "5", "?"], oct.Types.Select(t => t.Code));                  // codes 4/5 separate, unknown last and retained
        Assert.Equal("ไม่ระบุประเภท", oct.Type("?")!.Name); Assert.Equal(9m, oct.Type("?")!.EndingValue);
        Assert.Equal(oct.EndingValue, oct.TypeEndingTotal);
        var dec = r.Months.Single(m => m.MonthKey == "256812");
        Assert.False(dec.HasOpening); Assert.Null(dec.PreviousMonthKey);                    // Nov missing → not bridged to Oct
        Assert.Equal(10m, dec.ReceiveValue); Assert.Equal(200m, dec.EndingValue);
        Assert.False(r.Months.Single(m => m.MonthKey == "256909").HasOpening);
        Assert.Equal("256909", r.Latest!.MonthKey);
    }

    [Fact]
    public void Budget_state_reports_missing_year()
    {
        Assert.False(new DashboardBudget(2570, 0, null).HasBudget);
        Assert.True(new DashboardBudget(2569, 1, 3000000m).HasBudget);
    }

    [Fact]
    public void Coverage_state_handles_missing_period()
    {
        var none = new DashboardStockCoverage(null, null, null, null);
        Assert.False(none.HasPeriod);
        Assert.Null(none.Ratio);
        var some = new DashboardStockCoverage("2026", "08", 104897.85m, 40073.42m);
        Assert.True(some.HasPeriod);
        Assert.Equal(2.62m, Math.Round(some.Ratio!.Value, 2));
        Assert.Equal("ส.ค. 2569", some.DisplayPeriod);
        var zeroSales = new DashboardStockCoverage("2026", "08", 100m, 0m);
        Assert.Null(zeroSales.Ratio);
    }

    [Fact]
    public void Agreements_empty_state_is_not_a_zero_metric()
    {
        var none = new DashboardAgreements(0, 0m, 0);
        Assert.False(none.HasActiveAgreements);
        var some = new DashboardAgreements(2, 1500m, 1);
        Assert.True(some.HasActiveAgreements);
        Assert.Equal(1, some.UnvaluableCount);
    }

    [Fact]
    public void Section_result_captures_failure_without_throwing()
    {
        var ok = DashboardSection<int>.Ok(5);
        var failed = DashboardSection<int>.Failed("boom");
        Assert.True(ok.IsAvailable);
        Assert.Equal(5, ok.Value);
        Assert.False(failed.IsAvailable);
        Assert.Equal("boom", failed.Error);
        Assert.Equal(default, failed.Value);
    }

    [Fact]
    public void Snapshot_exposes_selected_year_and_as_of()
    {
        var asOf = new DateTime(2026, 9, 16, 14, 30, 0);
        var snap = new DashboardSnapshot
        {
            FiscalYear = 2569,
            FiscalYearNote = null,
            AsOf = asOf,
            Inventory = DashboardSection<InventorySummary>.Ok(new InventorySummary(267, 154357m, 104692.54m, [])),
            Reorder = DashboardSection<ReorderReport>.Failed("x"),
            PurchaseOrders = DashboardSection<PurchaseOrderReport>.Failed("x"),
            Receipts = DashboardSection<ReceiptReport>.Failed("x"),
            Budget = DashboardSection<DashboardBudget>.Failed("x"),
            Substock = DashboardSection<DashboardSubstock>.Failed("x"),
            Coverage = DashboardSection<DashboardStockCoverage>.Failed("x"),
            EdNed = DashboardSection<IReadOnlyList<DashboardEdNedRow>>.Failed("x"),
            Agreements = DashboardSection<DashboardAgreements>.Failed("x"),
            Movement = DashboardSection<DashboardProcessedMovement>.Failed("x"),
            ProcessTime = DashboardSection<IReadOnlyList<DashboardProcessTimeRow>>.Failed("x"),
            ItemTrend = DashboardSection<DashboardItemTrend?>.Ok(null),
        };
        Assert.Equal(2569, snap.FiscalYear);
        Assert.Equal(asOf, snap.AsOf);
        Assert.True(snap.Inventory.IsAvailable);
        Assert.Equal(10, snap.FailedSectionCount);
    }
}
