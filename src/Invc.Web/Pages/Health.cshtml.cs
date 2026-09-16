using Invc.Core.Diagnostics;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Invc.Web.Pages;

public class HealthModel(IDatabaseHealth health) : PageModel
{
    public DatabaseHealthResult Result { get; private set; } = default!;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Result = await health.CheckAsync(cancellationToken);
        if (!Result.IsHealthy)
        {
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        }
    }
}
