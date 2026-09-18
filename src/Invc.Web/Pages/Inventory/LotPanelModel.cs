using Invc.Core.Inventory;

namespace Invc.Web.Pages.Inventory;

/// <summary>
/// Data for the quick lot view under one Status row (partial _LotPanel): identity, header quantity (INV_MD) for the
/// total check, and the item's lots. Built from the Detail record (single expand) or from the one-statement
/// status-lots query (show all).
/// </summary>
public sealed record LotPanelModel(string WorkingCode, string DrugName, string? SaleUnit, decimal QtyOnHand, IReadOnlyList<InventoryLot> Lots)
{
    public decimal LotQtySum => Lots.Sum(l => l.QtyOnHand);
    public bool TotalMatches => LotQtySum == QtyOnHand;

    public static LotPanelModel From(InventoryItemDetail item)
        => new(item.WorkingCode, item.DrugName, item.SaleUnit, item.QtyOnHand, item.Lots);

    public static LotPanelModel From(InventoryItem item, IReadOnlyList<InventoryLot> lots)
        => new(item.WorkingCode, item.DrugName, item.SaleUnit, item.QtyOnHand, lots);
}
