using Invc.Core.Inventory;
using Invc.Core.Receipts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Invc.Web.Pages.Receipts;

/// <summary>
/// Non-PO receipts (รับเข้าอื่น / รับจาก CUP) — OTH_IVO / OTH_IVOC, one fiscal year at a time.
/// Statements per request: receive dates (for the year list), RCV_TYPE lookup, headers with line aggregates.
/// Type filter, breakdown and totals derive from the same loaded headers in Core.
/// </summary>
public class IndexModel(INonPoReceiptRepository receipts, ILogger<IndexModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true, Name = "fy")]
    public string? FiscalYearRaw { get; set; }

    [BindProperty(SupportsGet = true, Name = "type")]
    public string? TypeRaw { get; set; }

    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Keyword { get; set; }

    public int FiscalYear { get; private set; }
    public string? TypeCode { get; private set; }
    public IReadOnlyList<int> FiscalYearOptions { get; private set; } = [];
    public IReadOnlyList<ReceiptTypeOption> TypeOptions { get; private set; } = [];
    public string? PeriodNote { get; private set; }
    public ReceiptReport? Report { get; private set; }
    public bool HasError { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        TypeCode = ReceiptRules.NormalizeTypeCode(TypeRaw);
        Keyword = SearchKeyword.Normalize(Keyword);

        try
        {
            var years = await receipts.GetFiscalYearsWithDataAsync(cancellationToken);
            TypeOptions = await receipts.GetTypesAsync(cancellationToken);
            var defaultYear = ReceiptDefaultPeriod.Choose(years, DateTime.Today, out var note);
            FiscalYear = ReceiptRules.TryParseFiscalYear(FiscalYearRaw) ?? defaultYear;
            PeriodNote = FiscalYearRaw is null ? note : null;
            FiscalYearOptions = years.Append(FiscalYear).Distinct().OrderByDescending(y => y).ToList();

            var headers = await receipts.GetHeadersAsync(FiscalYear, Keyword, cancellationToken);
            Report = ReceiptReport.Build(FiscalYear, headers, TypeCode, Keyword);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Non-PO receipt list failed (fy: {Fy}, type: {Type})", FiscalYearRaw, TypeCode);
            HasError = true;
        }
    }
}
