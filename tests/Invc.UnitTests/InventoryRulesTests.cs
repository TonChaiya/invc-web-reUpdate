using Invc.Core.Inventory;

namespace Invc.UnitTests;

public class InventoryRulesTests
{
    // xUnit cannot convert inline ints to decimal?; take ints and convert here.
    private static decimal? D(int? v) => v;

    [Theory]
    [InlineData(300, 33, "9.09")]      // legacy: round(300/33, 2)
    [InlineData(0, 33, "0")]
    [InlineData(16, 33, "0.48")]       // 0 < x < 1 keeps the leading zero
    [InlineData(38100, 0, "N/A")]      // rate 0 → N/A
    [InlineData(38200, null, "N/A")]   // rate NULL → N/A
    [InlineData(10733, 10733, "1")]
    public void MonthsOfStock_matches_legacy_INV_Status(int qty, int? rate, string expected)
        => Assert.Equal(expected, InventoryRules.MonthsOfStock(qty, D(rate)));

    [Theory]
    [InlineData(0, 100, 100)]     // reorder 0 → fall back to MIN
    [InlineData(null, 100, 100)]  // reorder NULL → fall back to MIN
    [InlineData(150, 100, 150)]
    [InlineData(0, null, 0)]
    public void EffectiveReorderPoint_falls_back_to_min_level(int? reorder, int? min, int expectedRop)
        => Assert.Equal(expectedRop, InventoryRules.EffectiveReorderPoint(D(reorder), D(min)));

    [Theory]
    [InlineData(50, 100, 100, ReorderStatus.Red)]       // below MIN
    [InlineData(100, 100, 150, ReorderStatus.Yellow)]   // MIN ≤ stock < ROP
    [InlineData(149, 100, 150, ReorderStatus.Yellow)]
    [InlineData(150, 100, 150, ReorderStatus.Green)]
    [InlineData(100, 100, 100, ReorderStatus.Green)]    // ROP == MIN → yellow bucket unreachable (legacy behaviour)
    [InlineData(0, 0, 0, ReorderStatus.Green)]          // no MIN/ROP set → green (legacy behaviour)
    [InlineData(null, null, null, ReorderStatus.Green)]
    public void ClassifyReorder_matches_legacy_INV_Report_Purchase(int? qty, int? min, int? rop, ReorderStatus expected)
        => Assert.Equal(expected, InventoryRules.ClassifyReorder(D(qty), D(min), D(rop)));

    [Theory]
    [InlineData(50, 300, -250)]    // legacy shows negative suggestions when MAX < stock
    [InlineData(16100, 10000, 6100)]
    [InlineData(null, 5, -5)]
    public void SuggestedOrderQty_is_ceiling_of_max_minus_stock(int? max, int? qty, int expected)
        => Assert.Equal(expected, InventoryRules.SuggestedOrderQty(D(max), D(qty)));

    [Fact]
    public void SuggestedOrderQty_rounds_fractions_up()
        => Assert.Equal(3m, InventoryRules.SuggestedOrderQty(10.5m, 8m));

    [Theory]
    [InlineData("2026-09-17", ExpiryStatus.Expired)]        // yesterday
    [InlineData("2026-09-18", ExpiryStatus.Within1Month)]   // today is not yet expired
    [InlineData("2026-10-18", ExpiryStatus.Within1Month)]   // exactly one month
    [InlineData("2026-10-19", ExpiryStatus.Within3Months)]
    [InlineData("2026-12-18", ExpiryStatus.Within3Months)]  // exactly three months
    [InlineData("2026-12-19", ExpiryStatus.Ok)]
    [InlineData(null, ExpiryStatus.Unknown)]
    public void ClassifyExpiry_uses_owner_thresholds_expired_1_month_3_months(string? exp, ExpiryStatus expected)
        => Assert.Equal(expected, InventoryRules.ClassifyExpiry(exp is null ? null : DateTime.Parse(exp, System.Globalization.CultureInfo.InvariantCulture), new DateTime(2026, 9, 18, 14, 30, 0)));

    [Fact]
    public void DrugNameOrder_sorts_case_insensitively_latin_then_thai_with_blank_names_last_and_code_tiebreak()
    {
        var items = new[]
        {
            ("2000001", "ยาน้ำแก้ไอ"), ("1000030", "amoxicillin 500 mg tab"), ("1000010", "Acyclovir 400 mg tab"),
            ("1000020", (string?)null), ("1000011", "Acyclovir 400 mg tab"), ("3000860", "ซอง sterile 6 นิ้ว"),
        };
        var sorted = DrugNameOrder.Sort(items, i => i.Item2, i => i.Item1).Select(i => i.Item1).ToArray();
        Assert.Equal(["1000010", "1000011", "1000030", "3000860", "2000001", "1000020"], sorted);
    }

    // Quick lot view: each lot keeps its own PACK_RATIO (never merged across lots).
    [Theory]
    [InlineData(200, 100, "2 × 100")]
    [InlineData(250, 100, "2 × 100 + 50")]
    [InlineData(150, 50, "3 × 50")]
    [InlineData(40, 100, "40")]          // less than one pack → remainder only
    [InlineData(1000, 1000, "1 × 1,000")]
    public void PackExpression_describes_packs_per_lot(int qty, int ratio, string expected)
        => Assert.Equal(expected, InventoryRules.PackExpression(qty, ratio));

    [Theory]
    [InlineData(200, 1)]      // ratio 1 → "200 × 1" would be noise
    [InlineData(200, 0)]
    [InlineData(200, -5)]
    [InlineData(0, 100)]
    public void PackExpression_is_null_when_ratio_or_qty_is_not_meaningful(int qty, int ratio)
        => Assert.Null(InventoryRules.PackExpression(qty, ratio));
}
