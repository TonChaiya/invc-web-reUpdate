using Invc.Core.Inventory;
using Invc.Infrastructure.Inventory;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Invc.Web.Pages.Inventory;

/// <summary>
/// Read-only item detail with lots (port of the useful part of legacy chkstock.asp).
/// Route: /Inventory/Detail/{workingCode}. Two SQL statements per request (header, lots).
/// </summary>
public class DetailModel(IInventoryRepository inventory, ILogger<DetailModel> logger) : PageModel
{
    public InventoryItemDetail? Item { get; private set; }
    public string? WorkingCode { get; private set; }
    public bool HasError { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? workingCode, CancellationToken cancellationToken)
    {
        WorkingCode = workingCode?.Trim();
        if (!InventoryRepository.IsValidWorkingCode(WorkingCode))
        {
            return NotFound();
        }

        try
        {
            Item = await inventory.GetDetailAsync(WorkingCode!, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Inventory detail query failed (code: {WorkingCode})", WorkingCode);
            HasError = true;
            return Page();
        }

        return Item is null ? NotFound() : Page();
    }
}
