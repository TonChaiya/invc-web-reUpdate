namespace Invc.Core.Inventory;

/// <summary>
/// Item-level detail for /Inventory/Detail — the main-store record (dbo.INV_MD) plus its lots
/// (dbo.INV_MD_C). Only columns whose meaning was verified in Phase 0 are exposed.
/// </summary>
public sealed record InventoryItemDetail
{
    public required string WorkingCode { get; init; }
    public required string DrugName { get; init; }

    /// <summary>INV_MD.COMPOSITION (strength / composition text).</summary>
    public string? Composition { get; init; }

    /// <summary>INV_MD.DOSAGE_FORM.</summary>
    public string? DosageForm { get; init; }

    public string? SaleUnit { get; init; }
    public string? Location { get; init; }
    public string? HospCode { get; init; }
    public string? Ven { get; init; }
    public string? Abc { get; init; }

    /// <summary>TBLED_NED.EDNAME resolved from INV_MD.ED_NED (legacy default.asp join). Null when the code is unknown.</summary>
    public string? EdNedName { get; init; }

    /// <summary>True when INV_MD.NOUSE is set — the item is excluded from the status list.</summary>
    public bool IsInactive { get; init; }

    public decimal QtyOnHand { get; init; }

    /// <summary>INV_MD.TOTAL_VALUE — verified in Phase 0 to equal SUM(INV_MD_C.LOT_VALUE) for active items.</summary>
    public decimal TotalValue { get; init; }

    public decimal? RatePerMonth { get; init; }
    public decimal BorrowableQty { get; init; }

    public string MonthsOfStockDisplay => InventoryRules.MonthsOfStock(QtyOnHand, RatePerMonth);

    public required IReadOnlyList<InventoryLot> Lots { get; init; }

    /// <summary>Reconciliation of the header quantity/value against the lot rows.</summary>
    public LotReconciliation Reconciliation => LotReconciliation.From(QtyOnHand, TotalValue, Lots);
}

/// <summary>
/// One row of dbo.INV_MD_C. Fields follow the legacy chkstock.asp lot table
/// (trade name, packs, pack ratio, expiry, lot no., location, vendor, manufacturer) plus LOT_VALUE.
/// INV_MD_C.RECORD_STATUS and DISP_FIRST are intentionally not exposed: their semantics are UNRESOLVED.
/// </summary>
public sealed record InventoryLot
{
    /// <summary>INV_MD_C.PACK_RATIO — sale units per pack.</summary>
    public decimal PackRatio { get; init; }

    /// <summary>INV_MD_C.QTY_ON_HAND in sale units.</summary>
    public decimal QtyOnHand { get; init; }

    public DateTime? ExpiredDate { get; init; }
    public string? LotNo { get; init; }
    public string? Location { get; init; }

    /// <summary>INV_MD_C.LOT_VALUE.</summary>
    public decimal? LotValue { get; init; }

    public string? VendorCode { get; init; }
    public string? VendorName { get; init; }
    public string? ManufacCode { get; init; }
    public string? ManufacName { get; init; }

    /// <summary>DRUG_VN.TRADE_NAME matched on (WORKING_CODE, PACK_RATIO, VENDOR_CODE, MANUFAC_CODE) as in chkstock.asp.</summary>
    public string? TradeName { get; init; }

    /// <summary>
    /// Legacy chkstock.asp "คงเหลือ" column: QTY_ON_HAND / PACK_RATIO (packs). Null when PACK_RATIO is 0
    /// (the legacy page silently reused the previous row's value in that case).
    /// </summary>
    public decimal? Packs => InventoryRules.PacksOnHand(QtyOnHand, PackRatio);
}

/// <summary>Header-vs-lots reconciliation (parity check A10).</summary>
public sealed record LotReconciliation(
    int LotCount,
    decimal HeaderQty,
    decimal LotQtySum,
    decimal HeaderValue,
    decimal LotValueSum)
{
    public decimal QtyDelta => HeaderQty - LotQtySum;
    public decimal ValueDelta => HeaderValue - LotValueSum;
    public bool QtyMatches => QtyDelta == 0m;
    public bool ValueMatches => Math.Abs(ValueDelta) < 0.005m;
    public bool IsConsistent => QtyMatches && ValueMatches;

    public static LotReconciliation From(decimal headerQty, decimal headerValue, IReadOnlyList<InventoryLot> lots)
        => new(lots.Count, headerQty, lots.Sum(l => l.QtyOnHand), headerValue, lots.Sum(l => l.LotValue ?? 0m));
}
