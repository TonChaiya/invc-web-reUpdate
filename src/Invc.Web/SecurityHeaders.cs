namespace Invc.Web;

/// <summary>
/// Small, explicit set of application-owned response headers. A Content-Security-Policy is deliberately not set yet
/// (data-driven style attributes and the Bootstrap bundle need a verified policy first) — documented as future hardening.
/// </summary>
public static class SecurityHeaders
{
    public static readonly IReadOnlyDictionary<string, string> Values = new Dictionary<string, string>
    {
        ["X-Content-Type-Options"] = "nosniff",
        ["Referrer-Policy"] = "same-origin",
        ["X-Frame-Options"] = "SAMEORIGIN",
        ["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()",
    };

    /// <summary>Adds each header unless the host already supplied it (no duplicates).</summary>
    public static void Apply(IHeaderDictionary headers)
    {
        foreach (var (name, value) in Values)
        {
            if (!headers.ContainsKey(name))
            {
                headers[name] = value;
            }
        }
    }

    public static IApplicationBuilder UseInvcSecurityHeaders(this IApplicationBuilder app)
        => app.Use((context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                Apply(context.Response.Headers);
                return Task.CompletedTask;
            });
            return next();
        });
}
