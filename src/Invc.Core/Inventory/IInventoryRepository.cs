namespace Invc.Core.Inventory;

/// <summary>
/// Read-only access to the main-store inventory (dbo.INV_MD and related tables).
/// Implementations must issue SELECT statements only.
/// </summary>
public interface IInventoryRepository
{
    /// <summary>
    /// Inventory status list as shown by the legacy INV_Status.asp:
    /// active items (NOUSE IS NULL), optional keyword filter over drug name,
    /// composition, hospital code and working code, ordered by numeric working code.
    /// The keyword must already be normalised with <see cref="SearchKeyword.Normalize"/>.
    /// </summary>
    Task<IReadOnlyList<InventoryItem>> GetStatusAsync(string? keyword, CancellationToken cancellationToken = default);

    /// <summary>Headline totals for the active inventory (NOUSE IS NULL): count, quantity, value.</summary>
    Task<InventorySummary> GetSummaryAsync(CancellationToken cancellationToken = default);

    /// <summary>Item header plus lots for one working code; null when the code does not exist (active or not).</summary>
    Task<InventoryItemDetail?> GetDetailAsync(string workingCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every INV_MD_C lot of the items the Status list shows (same active/keyword filter), ordered by item then expiry —
    /// one SELECT for the "show all lots" action of the Status page. Never used during the list render itself.
    /// </summary>
    Task<IReadOnlyList<InventoryLot>> GetStatusLotsAsync(string? keyword, CancellationToken cancellationToken = default);

    /// <summary>
    /// Set-based header-vs-lot reconciliation over active items (parity check A10):
    /// one row per active item with lot count and lot sums.
    /// </summary>
    Task<IReadOnlyList<ItemLotTotals>> GetLotTotalsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Aggregate figures over active items (INV_MD.NOUSE IS NULL). The headline totals are the sums of the
/// ED/NED groups, so both views always agree.
/// </summary>
public sealed record InventorySummary(int ActiveItemCount, decimal TotalQtyOnHand, decimal TotalValue, IReadOnlyList<EdNedGroup> Groups)
{
    public static InventorySummary FromGroups(IReadOnlyList<EdNedGroup> groups)
        => new(groups.Sum(g => g.ItemCount), groups.Sum(g => g.TotalQtyOnHand), groups.Sum(g => g.TotalValue), groups);
}

/// <summary>Active items grouped by INV_MD.ED_NED (TBLED_NED: 1 ED, 2 NED, 3 MES, 4 EA, 5 SAM). Name is null for unknown codes.</summary>
public sealed record EdNedGroup(string? EdNedCode, string? EdNedName, string? EdNedShortName, int ItemCount, decimal TotalQtyOnHand, decimal TotalValue)
{
    public string DisplayName => EdNedName ?? (EdNedCode is null ? "ไม่ระบุ" : $"รหัส {EdNedCode} (ไม่พบในตาราง)");
}

/// <summary>Per-item lot totals used for reconciliation against INV_MD.</summary>
public sealed record ItemLotTotals(string WorkingCode, decimal HeaderQty, decimal HeaderValue, int LotCount, decimal LotQtySum, decimal LotValueSum)
{
    public bool QtyMatches => HeaderQty == LotQtySum;
    public bool ValueMatches => Math.Abs(HeaderValue - LotValueSum) < 0.005m;
}
