using System.Diagnostics;
using Dapper;
using Invc.Core.Diagnostics;

namespace Invc.Infrastructure.AppData;

/// <summary>SELECT-only probe of MySQL `invc_web`: version, schema_version, last Borrow mirror sync. Never throws — every failure becomes a state.</summary>
public sealed class AppDatabaseHealth(IAppDbConnectionFactory connections) : IAppDatabaseHealth
{
    public async Task<AppDatabaseHealthResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await using var c = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var version = await c.ExecuteScalarAsync<string>(new CommandDefinition("SELECT VERSION()", commandTimeout: connections.CommandTimeoutSeconds, cancellationToken: cancellationToken)).ConfigureAwait(false);
            var schema = await c.ExecuteScalarAsync<int?>(new CommandDefinition("SELECT MAX(version) FROM schema_version", commandTimeout: connections.CommandTimeoutSeconds, cancellationToken: cancellationToken)).ConfigureAwait(false);
            var synced = await c.ExecuteScalarAsync<DateTime?>(new CommandDefinition("SELECT MAX(last_synced_at) FROM borrow_source_bill", commandTimeout: connections.CommandTimeoutSeconds, cancellationToken: cancellationToken)).ConfigureAwait(false);
            return new AppDatabaseHealthResult(AppDatabaseState.Up, connections.DatabaseName, version, schema, synced, sw.Elapsed, null);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("is missing", StringComparison.Ordinal))
        {
            return new AppDatabaseHealthResult(AppDatabaseState.NotConfigured, null, null, null, null, sw.Elapsed, ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new AppDatabaseHealthResult(AppDatabaseState.Down, null, null, null, null, sw.Elapsed, ex.GetType().Name + ": " + ex.Message);
        }
    }
}
