using Invc.Core.Inventory;
using Invc.Core.Reorder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Invc.Web.Pages.Reorder;

/// <summary>
/// Print-friendly reorder report — port of INV_Report_Purchase_Print.asp. Same repository, same
/// <see cref="ReorderReport"/> construction as the screen page, so the row set and values are identical.
/// </summary>
public class PrintModel(IReorderRepository reorder, ILogger<PrintModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true, Name = "status")]
    public string? StatusRaw { get; set; }

    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Keyword { get; set; }

    /// <summary>ประเภทเวชภัณฑ์ (INV_MD.ED_NED) — the page's one category control; null = ทุกประเภท.</summary>
    [BindProperty(SupportsGet = true, Name = "type")]
    public string? TypeRaw { get; set; }

    public IReadOnlyList<ItemType> ItemTypes { get; private set; } = [];
    public string? ItemTypeCode { get; private set; }
    public ItemType? SelectedItemType => ItemTypeCode is null ? null : ItemTypes.FirstOrDefault(t => t.Code == ItemTypeCode);

    public ReorderStatusFilter Filter { get; private set; } = ReorderStatusFilter.Red;
    public ReorderReport? Report { get; private set; }
    public bool HasError { get; private set; }
    public DateTime GeneratedAt { get; } = DateTime.Now;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Filter = ReorderStatusFilterExtensions.Parse(StatusRaw);
        Keyword = SearchKeyword.Normalize(Keyword);
        try
        {
            ItemTypes = await reorder.GetItemTypesAsync(cancellationToken);
            ItemTypeCode = ItemType.NormalizeCode(TypeRaw, ItemTypes);
            var eligible = await reorder.GetEligibleAsync(Keyword, cancellationToken);
            Report = ReorderReport.Build(eligible, Filter, Keyword, ItemTypeCode);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Reorder print query failed (status: {Status})", Filter);
            HasError = true;
        }
    }
}
