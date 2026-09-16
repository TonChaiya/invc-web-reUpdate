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
}
