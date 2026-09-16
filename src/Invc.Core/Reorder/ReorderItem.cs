using Invc.Core.Inventory;

namespace Invc.Core.Reorder;

/// <summary>
/// One row of the reorder recommendation report — the port of legacy
/// <c>INV_Report_Purchase.asp</c> (a reorder report over <c>dbo.INV_MD</c>, NOT a purchase-order report).
/// Raw source values are kept as read; every derived value goes through <see cref="InventoryRules"/>
/// so the rules exist in exactly one place.
/// </summary>
public sealed record ReorderItem
{
    public required string WorkingCode { get; init; }
    public required string DrugName { get; init; }

    /// <summary>INV_MD.QTY_ON_HAND (sale units); NULL in the database is read as null and treated as 0 by the rules.</summary>
    public decimal? QtyOnHand { get; init; }

    /// <summary>INV_MD.MIN_LEVEL as stored (Access maintains it; 0/NULL means "not configured").</summary>
    public decimal? MinLevel { get; init; }

    /// <summary>INV_MD.REORDER_QTY as stored.</summary>
    public decimal? ReorderQty { get; init; }

    /// <summary>INV_MD.MAX_LEVEL as stored.</summary>
    public decimal? MaxLevel { get; init; }

    public string? SaleUnit { get; init; }
    public string? Location { get; init; }
    public decimal? RatePerMonth { get; init; }

    /// <summary>Legacy: <c>If reorder = 0 Then reorder = minLv</c>.</summary>
    public decimal EffectiveReorderPoint => InventoryRules.EffectiveReorderPoint(ReorderQty, MinLevel);

    /// <summary>Legacy red / yellow / green bucket.</summary>
    public ReorderStatus Status => InventoryRules.ClassifyReorder(QtyOnHand, MinLevel, ReorderQty);

    /// <summary>Legacy raw value <c>CEILING(MAX_LEVEL − QTY_ON_HAND)</c> — may be negative; never clamped here.</summary>
    public decimal SuggestedOrderQty => InventoryRules.SuggestedOrderQty(MaxLevel, QtyOnHand);

    /// <summary>Months of stock (same rule as the Inventory Status page).</summary>
    public string MonthsOfStockDisplay => InventoryRules.MonthsOfStock(QtyOnHand ?? 0m, RatePerMonth);

    /// <summary>True when MIN_LEVEL and MAX_LEVEL are both 0/NULL — the thresholds were never configured in Access.</summary>
    public bool HasNoThresholds => (MinLevel ?? 0m) == 0m && (MaxLevel ?? 0m) == 0m;
}

/// <summary>Status filter accepted by the reorder pages. Anything unrecognised normalises to <see cref="Red"/> (legacy default).</summary>
public enum ReorderStatusFilter
{
    Red,
    Yellow,
    Green,
    All,
}

public static class ReorderStatusFilterExtensions
{
    /// <summary>Parses the query-string value; null/blank/unknown → Red, matching the legacy default.</summary>
    public static ReorderStatusFilter Parse(string? value)
        => value?.Trim().ToLowerInvariant() switch
        {
            "yellow" => ReorderStatusFilter.Yellow,
            "green" => ReorderStatusFilter.Green,
            "all" => ReorderStatusFilter.All,
            _ => ReorderStatusFilter.Red,
        };

    public static string ToQueryValue(this ReorderStatusFilter filter) => filter.ToString().ToLowerInvariant();

    public static bool Matches(this ReorderStatusFilter filter, ReorderStatus status)
        => filter switch
        {
            ReorderStatusFilter.All => true,
            ReorderStatusFilter.Red => status == ReorderStatus.Red,
            ReorderStatusFilter.Yellow => status == ReorderStatus.Yellow,
            ReorderStatusFilter.Green => status == ReorderStatus.Green,
            _ => false,
        };

    public static string ThaiLabel(this ReorderStatusFilter filter)
        => filter switch
        {
            ReorderStatusFilter.Red => ReorderStatus.Red.ThaiLabel(),
            ReorderStatusFilter.Yellow => ReorderStatus.Yellow.ThaiLabel(),
            ReorderStatusFilter.Green => ReorderStatus.Green.ThaiLabel(),
            _ => "ทั้งหมด",
        };
}

public static class ReorderStatusExtensions
{
    /// <summary>Labels used by the legacy page: red ต้องสั่งซื้อทันที, yellow ใกล้ถึงจุดสั่งซื้อ, green มีสำรอง.</summary>
    public static string ThaiLabel(this ReorderStatus status)
        => status switch
        {
            ReorderStatus.Red => "ต้องสั่งซื้อทันที",
            ReorderStatus.Yellow => "ใกล้ถึงจุดสั่งซื้อ",
            ReorderStatus.Green => "มีสำรอง",
            _ => status.ToString(),
        };

    /// <summary>
    /// UI-only interpretation of the raw suggestion: an item that is not RED and whose raw suggestion is
    /// ≤ 0 needs nothing, so the page shows 0. RED items always show the raw legacy value.
    /// The raw value stays available as <see cref="ReorderItem.SuggestedOrderQty"/> for parity.
    /// </summary>
    public static decimal DisplaySuggestedQty(this ReorderItem item)
        => item.Status == ReorderStatus.Red ? item.SuggestedOrderQty : Math.Max(0m, item.SuggestedOrderQty);
}
