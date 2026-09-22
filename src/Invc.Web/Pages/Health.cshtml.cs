using Invc.Core.Diagnostics;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Invc.Web.Pages;

/// <summary>
/// Read-only connectivity probe. Technical details (server, login, SQL version, raw error) are shown in
/// Development only; Production shows status, timing and environment. Unhealthy → HTTP 503.
/// </summary>
public class HealthModel(IDatabaseHealth health, IAppDatabaseHealth appHealth, IWebHostEnvironment environment, ILogger<HealthModel> logger) : PageModel
{
    public HealthPresentation View { get; private set; } = default!;
    /// <summary>MySQL invc_web (Borrow workflow store) — probed independently; never affects the INV status or the HTTP code.</summary>
    public AppDatabaseHealthResult AppDb { get; private set; } = default!;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var result = await health.CheckAsync(cancellationToken);
        AppDb = await appHealth.CheckAsync(cancellationToken);
        if (AppDb.State != AppDatabaseState.Up) logger.LogWarning("Application database (MySQL) probe: {State} {Error}", AppDb.State, AppDb.Error);
        View = HealthPresentation.From(result, environment.IsDevelopment(), environment.EnvironmentName);
        if (!result.IsHealthy)
        {
            logger.LogError("Database health probe failed after {ElapsedMs} ms: {Error}", result.Elapsed.TotalMilliseconds, result.Error);
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        }
    }
}
