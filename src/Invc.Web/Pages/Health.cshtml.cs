using Invc.Core.Diagnostics;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Invc.Web.Pages;

/// <summary>
/// Read-only connectivity probe. Technical details (server, login, SQL version, raw error) are shown in
/// Development only; Production shows status, timing and environment. Unhealthy → HTTP 503.
/// </summary>
public class HealthModel(IDatabaseHealth health, IWebHostEnvironment environment, ILogger<HealthModel> logger) : PageModel
{
    public HealthPresentation View { get; private set; } = default!;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var result = await health.CheckAsync(cancellationToken);
        View = HealthPresentation.From(result, environment.IsDevelopment(), environment.EnvironmentName);
        if (!result.IsHealthy)
        {
            logger.LogError("Database health probe failed after {ElapsedMs} ms: {Error}", result.Elapsed.TotalMilliseconds, result.Error);
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        }
    }
}
