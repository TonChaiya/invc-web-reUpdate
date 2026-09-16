using Invc.Core.Diagnostics;
using Invc.Core.Inventory;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Invc.Web.Pages;

public class IndexModel(IInventoryRepository inventory, IDatabaseHealth health, ILogger<IndexModel> logger) : PageModel
{
    public InventorySummary? Summary { get; private set; }
    public DatabaseHealthResult? Health { get; private set; }
    public string? Error { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Health = await health.CheckAsync(cancellationToken);
        if (!Health.IsHealthy)
        {
            return;
        }

        try
        {
            Summary = await inventory.GetSummaryAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load inventory summary");
            Error = "ไม่สามารถอ่านข้อมูลสรุปคลังได้";
        }
    }
}
