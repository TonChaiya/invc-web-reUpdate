namespace Invc.Web;

/// <summary>
/// External (DDNS) read-only mode. The same immutable release is hosted twice: the internal IIS application uses
/// Windows Authentication and the full workflow, the external anonymous site sets
/// <c>Features:ExternalReadOnly=true</c> and may only read.
///
/// Enforcement is server-side and deliberately coarse: in read-only mode every unsafe HTTP method is refused, and the
/// pages whose only purpose is to write (<c>/Borrow/Return</c>, <c>/Borrow/ReturnBill</c>, <c>/Borrow/Correct</c>) are
/// refused even for GET. New write endpoints are therefore blocked by default — hiding the buttons is only cosmetic.
/// </summary>
public sealed class ExternalAccessMode(bool isReadOnly)
{
    /// <summary>Configuration key set by the external site's web.config (<c>Features__ExternalReadOnly</c>).</summary>
    public const string ConfigurationKey = "Features:ExternalReadOnly";

    public const string Banner = "กำลังดูผ่านระบบภายนอก — โหมดดูข้อมูลเท่านั้น";
    public const string DeniedMessage = "โหมดดูข้อมูลเท่านั้น (เข้าผ่านระบบภายนอก) — ไม่สามารถบันทึก แก้ไข หรือย้อนรายการได้ กรุณาใช้เครื่องในหน่วยงาน";

    /// <summary>Pages that exist only to mutate application-owned data; refused outright in read-only mode.</summary>
    private static readonly string[] MutationPages = ["/Borrow/Return", "/Borrow/ReturnBill", "/Borrow/Correct"];

    public bool IsReadOnly { get; } = isReadOnly;

    /// <summary>GET/HEAD/OPTIONS/TRACE are safe; everything else can change state.</summary>
    public static bool IsSafeMethod(string method) =>
        HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method);

    public static bool IsMutationPage(PathString path) =>
        MutationPages.Any(p => path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase));

    /// <summary>True when this request must be refused with 403 in read-only mode.</summary>
    public bool Denies(string method, PathString path) => IsReadOnly && (!IsSafeMethod(method) || IsMutationPage(path));
}

/// <summary>Refuses every state-changing request when the process runs in external read-only mode.</summary>
public static class ExternalReadOnlyGuard
{
    public static IApplicationBuilder UseInvcExternalReadOnlyGuard(this IApplicationBuilder app)
    {
        var mode = app.ApplicationServices.GetRequiredService<ExternalAccessMode>();
        if (!mode.IsReadOnly)
        {
            return app;
        }

        return app.Use(async (context, next) =>
        {
            if (!mode.Denies(context.Request.Method, context.Request.Path))
            {
                await next();
                return;
            }

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.Headers["Cache-Control"] = "no-store";
            await context.Response.WriteAsync(
                "<!doctype html><html lang=\"th\"><head><meta charset=\"utf-8\"><title>โหมดดูข้อมูลเท่านั้น</title></head>"
                + "<body style=\"font-family:system-ui,sans-serif;padding:2rem;max-width:40rem;margin:0 auto\">"
                + "<h1 style=\"font-size:1.25rem\">โหมดดูข้อมูลเท่านั้น</h1><p>"
                + ExternalAccessMode.DeniedMessage + "</p></body></html>");
        });
    }
}
