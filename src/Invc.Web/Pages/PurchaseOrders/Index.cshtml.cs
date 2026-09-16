using Invc.Core.Inventory;
using Invc.Core.PurchaseOrders;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Invc.Web.Pages.PurchaseOrders;

/// <summary>
/// Actual purchase orders (ใบสั่งซื้อ) for one fiscal year — port of PO_Search.asp / table_ipiss.asp /
/// ipiss_process.asp. Statements per request: open budget years, PO fiscal years, headers (one list; buckets,
/// status breakdown and the table derive from it in Core).
/// </summary>
public class IndexModel(IPurchaseOrderRepository purchaseOrders, ILogger<IndexModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true, Name = "fy")]
    public string? FiscalYearRaw { get; set; }

    [BindProperty(SupportsGet = true, Name = "bucket")]
    public string? BucketRaw { get; set; }

    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Keyword { get; set; }

    public int FiscalYear { get; private set; }
    public PurchaseOrderBucket Bucket { get; private set; } = PurchaseOrderBucket.Issued;
    public IReadOnlyList<int> FiscalYearOptions { get; private set; } = [];
    public string? FiscalYearNote { get; private set; }
    public PurchaseOrderReport? Report { get; private set; }
    public bool HasError { get; private set; }
    public DateTime Today { get; } = DateTime.Today;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Bucket = PurchaseOrderRules.ParseBucket(BucketRaw);
        Keyword = SearchKeyword.Normalize(Keyword);

        try
        {
            var open = await purchaseOrders.GetOpenBudgetYearsAsync(cancellationToken);
            var poYears = await purchaseOrders.GetPoFiscalYearsAsync(cancellationToken);
            var defaultYear = DefaultFiscalYear.Choose(open, poYears, Today, out var anomaly);
            FiscalYearNote = anomaly;

            FiscalYear = PurchaseOrderRules.TryParseFiscalYear(FiscalYearRaw) ?? defaultYear;
            FiscalYearOptions = poYears.Concat(open).Append(FiscalYear).Distinct().OrderByDescending(y => y).ToList();

            var headers = await purchaseOrders.GetHeadersAsync(FiscalYear, Keyword, cancellationToken);
            Report = PurchaseOrderReport.Build(FiscalYear, headers, Bucket, Keyword);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Purchase order list failed (fy: {Fy}, bucket: {Bucket})", FiscalYearRaw, Bucket);
            HasError = true;
        }
    }
}
