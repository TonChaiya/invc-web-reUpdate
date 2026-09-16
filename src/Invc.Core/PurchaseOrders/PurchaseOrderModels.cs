namespace Invc.Core.PurchaseOrders;

/// <summary>
/// One MS_PO header row for the list page. Lookup names are null when the lookup row is missing —
/// the PO itself is never dropped (legacy INNER JOINs did drop it).
/// </summary>
public sealed record PurchaseOrderSummary
{
    /// <summary>MS_PO.PO_NO — internal number; children in MS_PO_C join on it; first two digits = fiscal year.</summary>
    public required string PoNo { get; init; }

    /// <summary>MS_PO.REAL_PO — the document number users know; MS_IVO receipts join on it.</summary>
    public string? RealPo { get; init; }

    public string? DocNo { get; init; }
    public DateTime? PoDate { get; init; }
    public string? Status { get; init; }
    public string? StatusName { get; init; }
    public string? VendorCode { get; init; }
    public string? VendorName { get; init; }
    public string? BudgetType { get; init; }
    public string? BudgetName { get; init; }
    public string? BuyMethod { get; init; }
    public string? BuyMethodName { get; init; }
    public string? EdNed { get; init; }
    public string? EdNedName { get; init; }
    public decimal? TotalItem { get; init; }
    public decimal? TotalCost { get; init; }
    public DateTime? FirstReceiveDate { get; init; }
    public DateTime? BillIn { get; init; }
    public DateTime? BillOut { get; init; }
    public DateTime? BillEnd { get; init; }
    public DateTime? BillOutAcc { get; init; }
    public DateTime? BillOutFin { get; init; }
    public DateTime? BillEndAcc { get; init; }
    public string? ApproveNoFin { get; init; }
    public DateTime? BillPayFin { get; init; }

    /// <summary>User-facing identifier: REAL_PO when present, else PO_NO.</summary>
    public string DisplayNumber => string.IsNullOrWhiteSpace(RealPo) ? PoNo : RealPo!;

    public int? FiscalYear => PurchaseOrderRules.FiscalYearFromPoNumber(PoNo);
    public bool IsInBucket(PurchaseOrderBucket bucket) => PurchaseOrderRules.IsInBucket(Status, bucket);
    public int? ProcessDays(DateTime today) => PurchaseOrderRules.ProcessDays(FirstReceiveDate, today);
    public bool HasStatusName => !string.IsNullOrWhiteSpace(StatusName);
}

/// <summary>Count and value for one bucket or status code.</summary>
public sealed record PurchaseOrderAggregate(string Key, string? Label, int Count, decimal TotalValue);

/// <summary>
/// The list report for one fiscal year: every PO of that year loaded once, bucket figures and the per-status
/// breakdown derived from the same rows the table renders.
/// </summary>
public sealed class PurchaseOrderReport
{
    private PurchaseOrderReport(int fiscalYear, IReadOnlyList<PurchaseOrderSummary> orders, PurchaseOrderBucket bucket, string? keyword)
    {
        FiscalYear = fiscalYear;
        Orders = orders;
        Bucket = bucket;
        Keyword = keyword;
        Rows = orders.Where(o => o.IsInBucket(bucket)).ToList();
        Buckets = Enum.GetValues<PurchaseOrderBucket>()
            .ToDictionary(b => b, b =>
            {
                var rows = orders.Where(o => o.IsInBucket(b)).ToList();
                return new PurchaseOrderAggregate(b.ToQueryValue(), b.ThaiLabel(), rows.Count, rows.Sum(o => o.TotalCost ?? 0m));
            });
        StatusBreakdown = orders
            .GroupBy(o => (o.Status ?? string.Empty).Trim())
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new PurchaseOrderAggregate(g.Key, g.Select(o => o.StatusName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)),
                                                     g.Count(), g.Sum(o => o.TotalCost ?? 0m)))
            .ToList();
    }

    public int FiscalYear { get; }
    public string PoPrefix => PurchaseOrderRules.PoPrefix(FiscalYear);
    public PurchaseOrderBucket Bucket { get; }
    public string? Keyword { get; }

    /// <summary>All POs of the fiscal year (after the optional keyword filter).</summary>
    public IReadOnlyList<PurchaseOrderSummary> Orders { get; }

    /// <summary>POs in the selected bucket.</summary>
    public IReadOnlyList<PurchaseOrderSummary> Rows { get; }

    public IReadOnlyDictionary<PurchaseOrderBucket, PurchaseOrderAggregate> Buckets { get; }
    public IReadOnlyList<PurchaseOrderAggregate> StatusBreakdown { get; }

    public static PurchaseOrderReport Build(int fiscalYear, IReadOnlyList<PurchaseOrderSummary> orders, PurchaseOrderBucket bucket, string? keyword = null)
        => new(fiscalYear, orders, bucket, keyword);
}

/// <summary>MS_PO_C line (joined to MS_PO on PO_NO).</summary>
public sealed record PurchaseOrderLine
{
    public int? RunNo { get; init; }
    public required string WorkingCode { get; init; }
    public string? DrugName { get; init; }
    /// <summary>MS_PO_C.DRUG_PO — item description as printed on the PO.</summary>
    public string? DrugPo { get; init; }
    public decimal? QtyOrder { get; init; }
    public decimal? PackRatio1 { get; init; }
    public string? PoUnit { get; init; }
    public decimal? BuyUnitCost { get; init; }
    public decimal? BuyValue { get; init; }
    public decimal? QtyFree { get; init; }
    public decimal? PackRatio2 { get; init; }
    public string? Note { get; init; }

    /// <summary>Legacy QTY_ORDER / PACK_RATIO1 — null when the ratio is 0/NULL.</summary>
    public decimal? PacksOrdered => PurchaseOrderRules.Packs(QtyOrder, PackRatio1);
    public decimal? FreePacks => PurchaseOrderRules.Packs(QtyFree, PackRatio2);
}

/// <summary>MS_IVO receipt header (joined to MS_PO on REAL_PO).</summary>
public sealed record PurchaseOrderReceipt
{
    public string? ReceiveNo { get; init; }
    public string? InvoiceNo { get; init; }
    public DateTime? InvoiceDate { get; init; }
    public DateTime? DateReceive { get; init; }
    public decimal? TotalCost { get; init; }
    public decimal? TotalItem { get; init; }
    public DateTime? DateAcc { get; init; }
    public required IReadOnlyList<PurchaseOrderReceiptLine> Lines { get; init; }
}

/// <summary>MS_IVO_C line (joined to MS_IVO on RECEIVE_NO).</summary>
public sealed record PurchaseOrderReceiptLine
{
    public required string WorkingCode { get; init; }
    public string? DrugName { get; init; }
    public decimal? QtyOrder { get; init; }
    public decimal? PackRatio1 { get; init; }
    public decimal? BuyUnitCost { get; init; }
    public decimal? QtyFree { get; init; }
    public decimal? PackRatio2 { get; init; }
    public DateTime? ExpiredDate1 { get; init; }
    public string? Location1 { get; init; }
    public string? LotNo { get; init; }
    public string? ManufacCode { get; init; }

    public decimal? PacksReceived => PurchaseOrderRules.Packs(QtyOrder, PackRatio1);
}

/// <summary>Header vs. lines reconciliation (Step 18).</summary>
public sealed record PurchaseOrderReconciliation(decimal? HeaderItemCount, int LineCount, decimal? HeaderTotal, decimal LineTotal)
{
    public bool ItemCountMatches => HeaderItemCount.HasValue && HeaderItemCount.Value == LineCount;
    public bool TotalMatches => HeaderTotal.HasValue && Math.Abs(HeaderTotal.Value - LineTotal) < 0.005m;
    public decimal TotalDelta => (HeaderTotal ?? 0m) - LineTotal;
    public bool IsConsistent => ItemCountMatches && TotalMatches;
}

public sealed record PurchaseOrderDetail
{
    public required PurchaseOrderSummary Header { get; init; }
    public required IReadOnlyList<PurchaseOrderLine> Lines { get; init; }
    public required IReadOnlyList<PurchaseOrderReceipt> Receipts { get; init; }

    public PurchaseOrderReconciliation Reconciliation
        => new(Header.TotalItem, Lines.Count, Header.TotalCost, Lines.Sum(l => l.BuyValue ?? 0m));
}

public interface IPurchaseOrderRepository
{
    /// <summary>BUDGET.year values with BudgetOpen = 'O' (read only), ascending.</summary>
    Task<IReadOnlyList<int>> GetOpenBudgetYearsAsync(CancellationToken cancellationToken = default);

    /// <summary>Fiscal years present in MS_PO (from the PO_NO prefix), descending.</summary>
    Task<IReadOnlyList<int>> GetPoFiscalYearsAsync(CancellationToken cancellationToken = default);

    /// <summary>All MS_PO headers whose PO_NO starts with the fiscal-year prefix, optionally keyword-filtered.</summary>
    Task<IReadOnlyList<PurchaseOrderSummary>> GetHeadersAsync(int fiscalYear, string? keyword, CancellationToken cancellationToken = default);

    /// <summary>Header + lines + receipts for one REAL_PO; null when not found or the identifier is invalid.</summary>
    Task<PurchaseOrderDetail?> GetDetailAsync(string realPo, CancellationToken cancellationToken = default);

    /// <summary>Distinct non-null BILLOUT dates for the fiscal year (legacy date selector; parity C9).</summary>
    Task<IReadOnlyList<DateTime>> GetBillOutDatesAsync(int fiscalYear, CancellationToken cancellationToken = default);
}

/// <summary>Deterministic default fiscal-year choice (Step 5).</summary>
public static class DefaultFiscalYear
{
    /// <summary>
    /// Exactly one open budget year → that year. None → the latest fiscal year present in MS_PO, else the current
    /// Thai fiscal year. Several open years → the latest of them, and <paramref name="anomaly"/> is set so the UI can say so.
    /// </summary>
    public static int Choose(IReadOnlyList<int> openBudgetYears, IReadOnlyList<int> poFiscalYears, DateTime today, out string? anomaly)
    {
        anomaly = null;
        if (openBudgetYears.Count == 1)
        {
            return openBudgetYears[0];
        }

        if (openBudgetYears.Count > 1)
        {
            anomaly = $"BUDGET มีปีที่เปิดอยู่ (BudgetOpen='O') มากกว่าหนึ่งปี: {string.Join(", ", openBudgetYears)} — ใช้ปีล่าสุด";
            return openBudgetYears.Max();
        }

        if (poFiscalYears.Count > 0)
        {
            anomaly = "BUDGET ไม่มีปีที่เปิดอยู่ — ใช้ปีงบประมาณล่าสุดที่มีใบสั่งซื้อ";
            return poFiscalYears.Max();
        }

        anomaly = "BUDGET ไม่มีปีที่เปิดอยู่และยังไม่มีใบสั่งซื้อ — ใช้ปีงบประมาณปัจจุบัน";
        return Common.ThaiFiscalYear.FromDate(today);
    }
}
