namespace Invc.Core.Diagnostics;

/// <summary>
/// What the /Health page is allowed to show. Development keeps the technical diagnostics; every other environment
/// receives status, timing and environment identification only — never server/instance name, login, SQL version,
/// raw exception text or connection details.
/// </summary>
public sealed record HealthPresentation(
    bool IsHealthy,
    string StatusText,
    string EnvironmentName,
    string ApplicationName,
    TimeSpan Elapsed,
    bool ShowTechnicalDetails,
    string? ServerName,
    string? DatabaseName,
    string? LoginName,
    string? ProductVersion,
    string? Error)
{
    public const string GenericFailureMessage = "ไม่สามารถเชื่อมต่อฐานข้อมูลได้ในขณะนี้ กรุณาติดต่อผู้ดูแลระบบ (รายละเอียดถูกบันทึกใน log ของเซิร์ฟเวอร์)";

    public static HealthPresentation From(DatabaseHealthResult result, bool isDevelopment, string environmentName, string applicationName = "INVC Web")
    {
        var status = result.IsHealthy ? "ปกติ (SELECT 1 สำเร็จ)" : "ล้มเหลว";
        return isDevelopment
            ? new HealthPresentation(result.IsHealthy, status, environmentName, applicationName, result.Elapsed, true,
                result.ServerName, result.DatabaseName, result.LoginName, result.ProductVersion, result.Error)
            : new HealthPresentation(result.IsHealthy, status, environmentName, applicationName, result.Elapsed, false,
                null, null, null, null, result.IsHealthy ? null : GenericFailureMessage);
    }
}
