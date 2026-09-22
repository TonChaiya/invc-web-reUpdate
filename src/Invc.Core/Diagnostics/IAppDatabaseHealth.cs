namespace Invc.Core.Diagnostics;

/// <summary>Probe of the application-owned MySQL database (`invc_web`, Borrow workflow store). Independent of the INV probe.</summary>
public interface IAppDatabaseHealth
{
    Task<AppDatabaseHealthResult> CheckAsync(CancellationToken cancellationToken = default);
}

public enum AppDatabaseState { Up, Down, NotConfigured }

/// <param name="LastMirrorSyncAt">MAX(last_synced_at) of the Borrow mirror, when the store is reachable.</param>
public sealed record AppDatabaseHealthResult(AppDatabaseState State, string? DatabaseName, string? ServerVersion, int? SchemaVersion, DateTime? LastMirrorSyncAt, TimeSpan Elapsed, string? Error)
{
    public string StatusText => State switch
    {
        AppDatabaseState.Up => "ปกติ",
        AppDatabaseState.NotConfigured => "ยังไม่ตั้งค่า",
        _ => "ล้มเหลว",
    };
}
