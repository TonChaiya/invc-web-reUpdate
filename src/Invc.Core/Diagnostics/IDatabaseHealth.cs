namespace Invc.Core.Diagnostics;

/// <summary>Read-only connectivity probe (SELECT 1 plus server/database identity).</summary>
public interface IDatabaseHealth
{
    Task<DatabaseHealthResult> CheckAsync(CancellationToken cancellationToken = default);
}

public sealed record DatabaseHealthResult(
    bool IsHealthy,
    string? ServerName,
    string? DatabaseName,
    string? LoginName,
    string? ProductVersion,
    TimeSpan Elapsed,
    string? Error);
