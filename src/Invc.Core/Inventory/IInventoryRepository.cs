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
    /// </summary>
    Task<IReadOnlyList<InventoryItem>> GetStatusAsync(string? keyword, CancellationToken cancellationToken = default);

    /// <summary>Headline totals for the active inventory (count, quantity, value).</summary>
    Task<InventorySummary> GetSummaryAsync(CancellationToken cancellationToken = default);
}

/// <summary>Aggregate figures over active items (INV_MD.NOUSE IS NULL).</summary>
public sealed record InventorySummary(int ActiveItemCount, decimal TotalQtyOnHand, decimal TotalValue);
