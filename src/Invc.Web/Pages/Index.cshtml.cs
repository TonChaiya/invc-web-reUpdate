using Invc.Core.Dashboard;
using Invc.Core.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Invc.Web.Pages;

/// <summary>
/// Operational dashboard (root route). Composes the verified modules through <see cref="DashboardService"/>;
/// when the database is unreachable the health probe short-circuits to a top-level unavailable state.
/// </summary>
public class IndexModel(DashboardService dashboard, IDatabaseHealth health, ILogger<IndexModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true, Name = "fy")]
    public string? FiscalYearRaw { get; set; }

    [BindProperty(SupportsGet = true, Name = "item")]
    public string? ItemRaw { get; set; }

    public DatabaseHealthResult? Health { get; private set; }
    public DashboardSnapshot? Snapshot { get; private set; }
    public string? ItemCode { get; private set; }
    public bool ItemInvalid { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Health = await health.CheckAsync(cancellationToken);
        if (!Health.IsHealthy)
        {
            return;
        }

        ItemCode = DashboardRules.NormalizeItemCode(ItemRaw);
        ItemInvalid = !string.IsNullOrWhiteSpace(ItemRaw) && ItemCode is null;
        var fy = DashboardRules.TryParseFiscalYear(FiscalYearRaw);

        try
        {
            Snapshot = await dashboard.BuildAsync(fy, ItemCode, DateTime.Now, cancellationToken);
            if (Snapshot.FailedSectionCount > 0)
            {
                logger.LogWarning("Dashboard rendered with {Count} unavailable section(s) (fy {Fy})", Snapshot.FailedSectionCount, Snapshot.FiscalYear);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Dashboard build failed (fy: {Fy})", FiscalYearRaw);
            Snapshot = null;
        }
    }
}
