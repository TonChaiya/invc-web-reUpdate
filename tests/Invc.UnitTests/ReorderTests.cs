using Invc.Core.Inventory;
using Invc.Core.Reorder;
using Invc.Infrastructure.Data;
using Invc.Infrastructure.Reorder;

namespace Invc.UnitTests;

/// <summary>Phase 3: reorder rules matrix, filter parsing, report aggregation, UI interpretation.</summary>
public class ReorderRuleMatrixTests
{
    private static decimal? D(int? v) => v;

    [Theory]
    // stock, min, rop, expected status
    [InlineData(null, null, null, ReorderStatus.Green)]   // everything null → 0 < 0 false → green
    [InlineData(null, 100, 100, ReorderStatus.Red)]       // null stock reads as 0 → below min
    [InlineData(50, null, 150, ReorderStatus.Yellow)]     // null min → 0 ≤ 50 < 150 → yellow
    [InlineData(50, 100, null, ReorderStatus.Red)]        // null rop falls back to min; 50 < 100 → red
    [InlineData(50, 100, 0, ReorderStatus.Red)]           // rop 0 fallback → red
    [InlineData(100, 100, 0, ReorderStatus.Green)]        // rop 0 → rop = min → stock == min == rop → green
    [InlineData(99, 100, 200, ReorderStatus.Red)]         // below min
    [InlineData(100, 100, 200, ReorderStatus.Yellow)]     // equal min, below rop
    [InlineData(150, 100, 200, ReorderStatus.Yellow)]     // between
    [InlineData(200, 100, 200, ReorderStatus.Green)]      // equal rop
    [InlineData(201, 100, 200, ReorderStatus.Green)]      // above rop
    [InlineData(-5, 0, 0, ReorderStatus.Red)]             // negative stock below min 0
    [InlineData(-5, null, null, ReorderStatus.Red)]
    [InlineData(0, 0, 0, ReorderStatus.Green)]            // unconfigured item with zero stock → green (legacy)
    public void Classify(int? stock, int? min, int? rop, ReorderStatus expected)
        => Assert.Equal(expected, InventoryRules.ClassifyReorder(D(stock), D(min), D(rop)));

    [Theory]
    [InlineData(null, null, 0)]
    [InlineData(null, 100, 100)]
    [InlineData(0, 100, 100)]
    [InlineData(0, null, 0)]
    [InlineData(150, 100, 150)]
    [InlineData(150, null, 150)]
    public void EffectiveReorderPoint(int? rop, int? min, int expected)
        => Assert.Equal(expected, InventoryRules.EffectiveReorderPoint(D(rop), D(min)));

    [Theory]
    [InlineData(null, null, 0)]
    [InlineData(null, 300, -300)]      // null max → negative suggestion equal to −stock
    [InlineData(500, null, 500)]
    [InlineData(500, 300, 200)]
    [InlineData(300, 500, -200)]       // negative raw value preserved
    [InlineData(0, 0, 0)]
    public void SuggestedOrderQty_integers(int? max, int? stock, int expected)
        => Assert.Equal(expected, InventoryRules.SuggestedOrderQty(D(max), D(stock)));

    [Theory]
    [InlineData("10.5", "8", "3")]        // ceiling of 2.5
    [InlineData("10", "8.25", "2")]       // ceiling of 1.75
    [InlineData("8", "10.5", "-2")]       // ceiling of −2.5 is −2 (toward +∞), as CEILING in SQL/VBScript's -Int(-x)
    [InlineData("0.1", "0", "1")]
    public void SuggestedOrderQty_fractional_ceiling(string max, string stock, string expected)
        => Assert.Equal(decimal.Parse(expected), InventoryRules.SuggestedOrderQty(decimal.Parse(max), decimal.Parse(stock)));
}

public class ReorderItemAndReportTests
{
    private static ReorderItem Item(string code, decimal? stock, decimal? min, decimal? rop, decimal? max)
        => new() { WorkingCode = code, DrugName = "x " + code, QtyOnHand = stock, MinLevel = min, ReorderQty = rop, MaxLevel = max };

    private static readonly IReadOnlyList<ReorderItem> Sample =
    [
        Item("1", 50, 100, 100, 150),     // red, suggest 100
        Item("2", 120, 100, 150, 200),    // yellow, suggest 80
        Item("3", 300, 100, 100, 150),    // green, suggest −150
        Item("4", 0, 0, 0, 0),            // green, unconfigured, suggest 0
        Item("5", 10, 20, 0, 0),          // red via fallback, MAX unset → suggest −10 (raw)
    ];

    [Fact]
    public void Report_counts_come_from_the_same_classified_list()
    {
        var r = ReorderReport.Build(Sample, ReorderStatusFilter.Red);
        Assert.Equal(5, r.EligibleCount);
        Assert.Equal(2, r.RedCount);
        Assert.Equal(1, r.YellowCount);
        Assert.Equal(2, r.GreenCount);
        Assert.Equal(r.EligibleCount, r.RedCount + r.YellowCount + r.GreenCount);
        Assert.Equal(new[] { "1", "5" }, r.Rows.Select(i => i.WorkingCode));
        Assert.Equal(100 + (-10), r.RedSuggestedTotal);
    }

    [Fact]
    public void Report_missing_threshold_counts()
    {
        var r = ReorderReport.Build(Sample, ReorderStatusFilter.All);
        Assert.Equal(1, r.MissingMinCount);   // item 4 only
        Assert.Equal(2, r.MissingMaxCount);   // items 4 and 5
    }

    [Theory]
    [InlineData(ReorderStatusFilter.Red, "1,5")]
    [InlineData(ReorderStatusFilter.Yellow, "2")]
    [InlineData(ReorderStatusFilter.Green, "3,4")]
    [InlineData(ReorderStatusFilter.All, "1,2,3,4,5")]
    public void Filter_selects_rows_without_changing_counts(ReorderStatusFilter filter, string expectedCodes)
    {
        var r = ReorderReport.Build(Sample, filter);
        Assert.Equal(expectedCodes, string.Join(",", r.Rows.Select(i => i.WorkingCode)));
        Assert.Equal((2, 1, 2), (r.RedCount, r.YellowCount, r.GreenCount));
    }

    [Fact]
    public void All_filter_is_the_union_of_the_three_buckets_in_order()
    {
        var all = ReorderReport.Build(Sample, ReorderStatusFilter.All).Rows.Select(i => i.WorkingCode).ToList();
        var union = new[] { ReorderStatusFilter.Red, ReorderStatusFilter.Yellow, ReorderStatusFilter.Green }
            .SelectMany(f => ReorderReport.Build(Sample, f).Rows.Select(i => i.WorkingCode))
            .OrderBy(c => c).ToList();
        Assert.Equal(union, all.OrderBy(c => c));
        Assert.Equal(Sample.Select(i => i.WorkingCode), all);   // preserves source order
    }

    [Theory]
    [InlineData("red", ReorderStatusFilter.Red)]
    [InlineData("RED", ReorderStatusFilter.Red)]
    [InlineData(" yellow ", ReorderStatusFilter.Yellow)]
    [InlineData("green", ReorderStatusFilter.Green)]
    [InlineData("all", ReorderStatusFilter.All)]
    [InlineData(null, ReorderStatusFilter.Red)]
    [InlineData("", ReorderStatusFilter.Red)]
    [InlineData("blue", ReorderStatusFilter.Red)]
    [InlineData("red'; DROP TABLE x", ReorderStatusFilter.Red)]
    public void Filter_parse_normalises_to_red_by_default(string? raw, ReorderStatusFilter expected)
        => Assert.Equal(expected, ReorderStatusFilterExtensions.Parse(raw));

    [Fact]
    public void Display_suggestion_shows_zero_for_non_red_negative_but_keeps_raw()
    {
        var green = Item("g", 300, 100, 100, 150);
        Assert.Equal(-150, green.SuggestedOrderQty);
        Assert.Equal(0, green.DisplaySuggestedQty());

        var red = Item("r", 10, 20, 0, 0);
        Assert.Equal(-10, red.SuggestedOrderQty);
        Assert.Equal(-10, red.DisplaySuggestedQty());   // red keeps the traceable legacy value

        var yellow = Item("y", 120, 100, 150, 200);
        Assert.Equal(80, yellow.DisplaySuggestedQty());
    }

    [Fact]
    public void Item_derived_values_and_labels()
    {
        var i = Item("5", 10, 20, 0, 0);
        Assert.Equal(20, i.EffectiveReorderPoint);
        Assert.Equal(ReorderStatus.Red, i.Status);
        Assert.False(i.HasNoThresholds);
        Assert.True(Item("4", 0, null, null, 0).HasNoThresholds);
        Assert.Equal("ต้องสั่งซื้อทันที", ReorderStatus.Red.ThaiLabel());
        Assert.Equal("ใกล้ถึงจุดสั่งซื้อ", ReorderStatus.Yellow.ThaiLabel());
        Assert.Equal("มีสำรอง", ReorderStatus.Green.ThaiLabel());
        Assert.Equal("ทั้งหมด", ReorderStatusFilter.All.ThaiLabel());
        Assert.Equal("N/A", i.MonthsOfStockDisplay);
    }

    [Fact]
    public void Yellow_bucket_is_empty_whenever_rop_equals_min()
    {
        // Production observation: REORDER_QTY == MIN_LEVEL for every configured item ⇒ MIN ≤ stock < ROP is empty.
        var items = Enumerable.Range(0, 500).Select(s => Item(s.ToString(), s, 100, 100, 150)).ToList();
        var r = ReorderReport.Build(items, ReorderStatusFilter.Yellow);
        Assert.Equal(0, r.YellowCount);
        Assert.Empty(r.Rows);
        Assert.Equal(100, r.RedCount);
        Assert.Equal(400, r.GreenCount);
    }
}

public class ReorderSqlGuardTests
{
    [Fact]
    public void Every_reorder_statement_passes_the_guard_and_uses_the_legacy_eligibility_filter()
    {
        var type = typeof(ReorderRepository).Assembly.GetType("Invc.Infrastructure.Reorder.ReorderSql")!;
        var fields = type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.FieldType == typeof(string) && f.Name != "EligibilityFilter").ToList();

        Assert.Equal(2, fields.Count);
        foreach (var f in fields)
        {
            var sql = (string)f.GetValue(null)!;
            Assert.Equal(sql, ReadOnlySql.Ensure(sql));
            Assert.DoesNotContain("SELECT *", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("(m.NOUSE IS NULL OR m.NOUSE = '')", sql, StringComparison.Ordinal);
            Assert.Contains("(m.OUT_OF_LIST IS NULL OR m.OUT_OF_LIST = '')", sql, StringComparison.Ordinal);
            Assert.DoesNotContain("ROP_EXCEPT", sql, StringComparison.OrdinalIgnoreCase);   // legacy did not filter it
        }
    }
}
