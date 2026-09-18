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

    /// <summary>Selected storage location (INV_MD.LOCATION) — the single category control of this page (owner request 2026-09-18).</summary>
    [BindProperty(SupportsGet = true, Name = "loc")]
    public string? Location { get; set; }

    public IReadOnlyList<string> Locations { get; private set; } = [];
    public IReadOnlyList<InventoryItem> Items { get; private set; } = [];
    public InventorySummary? Summary { get; private set; }
    public bool HasError { get; private set; }

    public bool IsFiltered => Keyword is not null || Location is not null;
    /// <summary>Compact summary of the selected context: count and quantity of the shown items (value is only known for the whole store).</summary>
    public decimal ShownQty => Items.Sum(i => i.QtyOnHand);

    /// <summary>
    /// Lazy lot panel for one medicine (GET /Inventory/Status?handler=Lots&amp;workingCode=…): rendered only when the
    /// user expands a row, never during the list render (no N+1). Reuses the Detail query path (header + lots,
    /// one lot SELECT) so the header quantity used for the total check is server-authoritative.
    /// </summary>
    public async Task<IActionResult> OnGetLotsAsync(string? workingCode, CancellationToken cancellationToken)
    {
        workingCode = workingCode?.Trim();
        if (!Invc.Infrastructure.Inventory.InventoryRepository.IsValidWorkingCode(workingCode))
        {
            return NotFound();
        }

        InventoryItemDetail? item;
        try
        {
            item = await inventory.GetDetailAsync(workingCode!, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Inventory lot panel query failed (code: {WorkingCode})", workingCode);
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        return item is null ? NotFound() : Partial("_LotPanel", LotPanelModel.From(item));
    }

    /// <summary>
    /// "แสดงล็อตทั้งหมด" (GET /Inventory/Status?handler=AllLots&amp;q=…): the lot panels of every item the list shows for the
    /// same keyword — two SELECTs in total (items + all their lots), never one per item. Items without lots get an
    /// empty panel so every row can expand.
    /// </summary>
    public async Task<IActionResult> OnGetAllLotsAsync(string? q, string? loc, CancellationToken cancellationToken)
    {
        var keyword = SearchKeyword.Normalize(q);
        var location = Invc.Infrastructure.Inventory.InventoryRepository.NormalizeLocation(loc);
        try
        {
            var items = await inventory.GetStatusAsync(keyword, location, cancellationToken);
            var lots = await inventory.GetStatusLotsAsync(keyword, location, cancellationToken);
            var byCode = lots.GroupBy(l => l.WorkingCode).ToDictionary(g => g.Key, g => (IReadOnlyList<InventoryLot>)g.ToList());
            var panels = items.Select(i => LotPanelModel.From(i, byCode.TryGetValue(i.WorkingCode, out var l) ? l : [])).ToList();
            return Partial("_LotPanels", panels);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Inventory all-lots query failed (keyword: {Keyword})", keyword);
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Keyword = SearchKeyword.Normalize(Keyword);
        Location = Invc.Infrastructure.Inventory.InventoryRepository.NormalizeLocation(Location);

        try
        {
            Summary = await inventory.GetSummaryAsync(cancellationToken);
            Locations = await inventory.GetLocationsAsync(cancellationToken);
            Items = await inventory.GetStatusAsync(Keyword, Location, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Inventory status query failed (keyword: {Keyword})", Keyword);
            HasError = true;
        }
    }
}
