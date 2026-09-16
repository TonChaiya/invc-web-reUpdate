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
    [InlineData("R", "O6900085", "RO", MovementDirection.Receive)]
    [InlineData("R", "S6900001", "RS", MovementDirection.Receive)]
    [InlineData("S", "S6900001", "SS", MovementDirection.Issue)]
    [InlineData("S", "O6900001", "SO", MovementDirection.Issue)]
    [InlineData("X", "Q1", "XQ", MovementDirection.Other)]
    [InlineData(null, null, "??", MovementDirection.Other)]
    [InlineData("S", "", "S?", MovementDirection.Issue)]
    public void MovementCategory_preserves_raw_key_and_maps_direction(string? status, string? number, string key, MovementDirection dir)
    {
        var c = MovementCategory.FromRaw(status, number);
        Assert.Equal(key, c.Key);
        Assert.Equal(dir, c.Direction);
        Assert.False(string.IsNullOrWhiteSpace(c.ThaiLabel));
    }

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
    [Fact]
    public void Movement_rows_group_into_months_and_directions_without_dropping_categories()
    {
        var rows = new List<DashboardMovementRow>
        {
            new("256810", "R", "O", 10, 1000m, 50m),
            new("256810", "S", "S", 20, 800m, 40m),
            new("256810", "S", "O", 1, 5m, 1m),
            new("256811", "R", "S", 2, 12m, 3m),
        };
        var months = DashboardMovement.Build(rows);

        Assert.Equal(2, months.Count);
        var oct = months.Single(m => m.MonthKey == "256810");
        Assert.Equal(1000m, oct.ReceiveValue);
        Assert.Equal(805m, oct.IssueValue);
        Assert.Equal(3, oct.Categories.Count);                          // RO, SS, SO all retained
        Assert.Equal(31, oct.Categories.Sum(c => c.Count));
        Assert.Equal(rows.Where(r => r.MonthKey == "256810").Sum(r => r.Value), oct.ReceiveValue + oct.IssueValue + oct.OtherValue);
        Assert.Equal("ต.ค. 2568", oct.DisplayMonth);
        Assert.Equal(new[] { "256811", "256810" }, months.Select(m => m.MonthKey));   // newest first
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
            Movement = DashboardSection<IReadOnlyList<DashboardMovementMonth>>.Failed("x"),
            ProcessTime = DashboardSection<IReadOnlyList<DashboardProcessTimeRow>>.Failed("x"),
            ItemTrend = DashboardSection<DashboardItemTrend?>.Ok(null),
        };
        Assert.Equal(2569, snap.FiscalYear);
        Assert.Equal(asOf, snap.AsOf);
        Assert.True(snap.Inventory.IsAvailable);
        Assert.Equal(10, snap.FailedSectionCount);
    }
}
