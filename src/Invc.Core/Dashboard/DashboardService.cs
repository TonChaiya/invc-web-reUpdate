using Invc.Core.Inventory;
using Invc.Core.PurchaseOrders;
using Invc.Core.Receipts;
using Invc.Core.Reorder;

namespace Invc.Core.Dashboard;

/// <summary>
/// Orchestrates the trusted module repositories/reports plus the dashboard-only analytics into one snapshot.
/// Each section is isolated: a failing optional section becomes <see cref="DashboardSection{T}.Failed"/> instead of
/// blanking the page. Statements run sequentially (small database; predictable load).
/// </summary>
public sealed class DashboardService(
    IInventoryRepository inventory,
    IReorderRepository reorder,
    IPurchaseOrderRepository purchaseOrders,
    INonPoReceiptRepository receipts,
    IDashboardAnalyticsRepository analytics)
{
    public const int TrendMonths = 12;

    /// <summary>
    /// Builds the snapshot. <paramref name="requestedFiscalYear"/> null → reuse the Purchase Order default-year rule
    /// (single open BUDGET year, else latest PO year, else current Thai FY).
    /// </summary>
    public async Task<DashboardSnapshot> BuildAsync(int? requestedFiscalYear, string? itemCode, DateTime now, CancellationToken ct = default)
    {
        var open = await purchaseOrders.GetOpenBudgetYearsAsync(ct);
        var poYears = await purchaseOrders.GetPoFiscalYearsAsync(ct);
        var defaultYear = DefaultFiscalYear.Choose(open, poYears, now, out var note);
        var fy = requestedFiscalYear ?? defaultYear;
        var options = poYears.Concat(open).Append(fy).Append(DashboardRules.FiscalYearOf(now)).Distinct().OrderByDescending(y => y).ToList();

        var inventorySection = await Section(() => inventory.GetSummaryAsync(ct));
        var reorderSection = await Section(async () => ReorderReport.Build(await reorder.GetEligibleAsync(null, ct), ReorderStatusFilter.All));
        var poSection = await Section(async () => PurchaseOrderReport.Build(fy, await purchaseOrders.GetHeadersAsync(fy, null, ct), PurchaseOrderBucket.Issued));
        var receiptSection = await Section(async () => ReceiptReport.Build(fy, await receipts.GetHeadersAsync(fy, null, ct), null, null));

        var budget = await Section(() => analytics.GetBudgetAsync(fy, ct));
        var substock = await Section(() => analytics.GetSubstockAsync(ct));
        var coverage = await Section(() => analytics.GetStockCoverageAsync(ct));
        var edNed = await Section(() => analytics.GetLegacyEdNedAsync(ct));
        var agreements = await Section(async () => Summarize(await analytics.GetActiveAgreementsAsync(now, ct)));
        var movement = await Section(async () => DashboardMovement.Build(await analytics.GetMovementAsync(fy, ct), await analytics.GetMovementByItemTypeAsync(fy, ct)));
        var processTime = await Section(() => analytics.GetProcessTimeAsync(fy, ct));
        var normalizedItem = DashboardRules.NormalizeItemCode(itemCode);
        var trend = normalizedItem is null
            ? DashboardSection<DashboardItemTrend?>.Ok(null)
            : await Section<DashboardItemTrend?>(() => analytics.GetItemTrendAsync(normalizedItem, TrendMonths, ct));

        return new DashboardSnapshot
        {
            FiscalYear = fy,
            FiscalYearNote = requestedFiscalYear is null ? note : null,
            FiscalYearOptions = options,
            AsOf = now,
            Inventory = inventorySection,
            Reorder = reorderSection,
            PurchaseOrders = poSection,
            Receipts = receiptSection,
            Budget = budget,
            Substock = substock,
            Coverage = coverage,
            EdNed = edNed,
            Agreements = agreements,
            Movement = movement,
            ProcessTime = processTime,
            ItemTrend = trend,
        };
    }

    /// <summary>D7 from raw active rows: count, Σ remaining (rows with a computable value), and rows that cannot be valued.</summary>
    public static DashboardAgreements Summarize(IReadOnlyList<DashboardAgreementRow> rows)
    {
        var values = rows.Select(r => DashboardRules.AgreementRemaining(r.AgreeQty, r.BuyQty, r.PackRatio, r.UnitPrice)).ToList();
        return new DashboardAgreements(rows.Count, values.Where(v => v.HasValue).Sum(v => v!.Value), values.Count(v => !v.HasValue));
    }

    private static async Task<DashboardSection<T>> Section<T>(Func<Task<T>> load)
    {
        try
        {
            return DashboardSection<T>.Ok(await load().ConfigureAwait(false));
        }
        catch (Exception ex)
        {
            // Message only — never SQL, connection strings or stack traces; the page logs the failure separately.
            return DashboardSection<T>.Failed(ex.GetType().Name);
        }
    }
}
