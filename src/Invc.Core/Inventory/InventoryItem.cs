namespace Invc.Core.Inventory;

/// <summary>
/// One row of the inventory status report. Mirrors the columns the legacy
/// <c>INV_Status.asp</c> displayed from <c>dbo.INV_MD</c> (plus the borrowable
/// quantity it computed from <c>VCAR</c>/<c>BORROW</c>).
/// All fields are read-only projections of production data.
/// </summary>
public sealed record InventoryItem
{
    /// <summary>INV_MD.WORKING_CODE — business key (nvarchar 7, digits in practice).</summary>
    public required string WorkingCode { get; init; }

    /// <summary>INV_MD.DRUG_NAME.</summary>
    public required string DrugName { get; init; }

    /// <summary>INV_MD.HOSP_CODE — HIS code; NULL for every item at the audited site.</summary>
    public string? HospCode { get; init; }

    /// <summary>INV_MD.VEN (V/E/N) — may be NULL.</summary>
    public string? Ven { get; init; }

    /// <summary>INV_MD.QTY_ON_HAND in sale units.</summary>
    public decimal QtyOnHand { get; init; }

    /// <summary>INV_MD.SALE_UNIT.</summary>
    public string? SaleUnit { get; init; }

    /// <summary>INV_MD.LOCATION (shelf group name).</summary>
    public string? Location { get; init; }

    /// <summary>INV_MD.RATE_PER_MONTH — monthly usage maintained by the Access application.</summary>
    public decimal? RatePerMonth { get; init; }

    /// <summary>
    /// Legacy "ยืมได้": VCAR.BORROW_QTY − SUM(BORROW.BORROW_QTY) for open vendor claims
    /// (CLOSE_STATUS &lt;&gt; 'C'). Zero when no claim exists.
    /// </summary>
    public decimal BorrowableQty { get; init; }

    /// <summary>Legacy "เหลือใช้ได้ (เดือน)" computed by <see cref="InventoryRules.MonthsOfStock"/>.</summary>
    public string MonthsOfStockDisplay => InventoryRules.MonthsOfStock(QtyOnHand, RatePerMonth);
}
