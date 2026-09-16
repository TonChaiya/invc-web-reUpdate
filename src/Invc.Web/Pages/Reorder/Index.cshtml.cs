using Invc.Core.Inventory;
using Invc.Core.Reorder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Invc.Web.Pages.Reorder;

/// <summary>
/// Reorder recommendations (คำแนะนำการสั่งซื้อ) — port of legacy INV_Report_Purchase.asp.
/// One SQL statement per request; classification, counts and filtering happen in Core.
/// </summary>
public class IndexModel(IReorderRepository reorder, ILogger<IndexModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true, Name = "status")]
    public string? StatusRaw { get; set; }

    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Keyword { get; set; }

    public ReorderStatusFilter Filter { get; private set; } = ReorderStatusFilter.Red;
    public ReorderReport? Report { get; private set; }
    public bool HasError { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Filter = ReorderStatusFilterExtensions.Parse(StatusRaw);
        Keyword = SearchKeyword.Normalize(Keyword);

        try
        {
            var eligible = await reorder.GetEligibleAsync(Keyword, cancellationToken);
            Report = ReorderReport.Build(eligible, Filter, Keyword);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Reorder query failed (status: {Status}, keyword: {Keyword})", Filter, Keyword);
            HasError = true;
        }
    }
}
