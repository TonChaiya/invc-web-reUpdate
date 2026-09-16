using Invc.Core.Inventory;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Invc.Web.Pages.Inventory;

/// <summary>
/// Port of the legacy INV_Status.asp (menu "สถานะคงคลัง").
/// Two SQL statements per request: the active-item summary and the (optionally filtered) list.
/// The list is rendered in full: production has a few hundred active items and the query is sub-second,
/// so server-side pagination was deliberately not added (docs/phase2-inventory-parity.md, Step 11).
/// </summary>
public class StatusModel(IInventoryRepository inventory, ILogger<StatusModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Keyword { get; set; }

    public IReadOnlyList<InventoryItem> Items { get; private set; } = [];
    public InventorySummary? Summary { get; private set; }
    public bool HasError { get; private set; }

    public bool IsFiltered => Keyword is not null;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Keyword = SearchKeyword.Normalize(Keyword);

        try
        {
            Summary = await inventory.GetSummaryAsync(cancellationToken);
            Items = await inventory.GetStatusAsync(Keyword, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Inventory status query failed (keyword: {Keyword})", Keyword);
            HasError = true;
        }
    }
}
