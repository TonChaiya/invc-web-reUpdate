using System.Diagnostics;
using Dapper;
using Invc.Core.Diagnostics;
using Invc.Infrastructure.Data;
using Microsoft.Extensions.Logging;

namespace Invc.Infrastructure.Diagnostics;

/// <summary>SELECT-only connectivity probe used by the /Health page and integration tests.</summary>
public sealed class DatabaseHealth(ISqlConnectionFactory connections, ILogger<DatabaseHealth> logger) : IDatabaseHealth
{
    private const string ProbeSql = """
        SELECT CAST(@@SERVERNAME AS nvarchar(128))                       AS ServerName,
               CAST(DB_NAME() AS nvarchar(128))                          AS DatabaseName,
               CAST(SUSER_SNAME() AS nvarchar(128))                      AS LoginName,
               CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(32))    AS ProductVersion
        """;

    public async Task<DatabaseHealthResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        var watch = Stopwatch.StartNew();
        try
        {
            await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var one = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                ReadOnlySql.Ensure("SELECT 1"), commandTimeout: 5, cancellationToken: cancellationToken)).ConfigureAwait(false);
            if (one != 1)
            {
                return new DatabaseHealthResult(false, null, null, null, null, watch.Elapsed, "SELECT 1 returned an unexpected value.");
            }

            var identity = await connection.QuerySingleAsync<ProbeRow>(new CommandDefinition(
                ReadOnlySql.Ensure(ProbeSql), commandTimeout: 5, cancellationToken: cancellationToken)).ConfigureAwait(false);

            return new DatabaseHealthResult(true, identity.ServerName, identity.DatabaseName, identity.LoginName,
                identity.ProductVersion, watch.Elapsed, null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "INV database health probe failed");
            return new DatabaseHealthResult(false, null, null, null, null, watch.Elapsed, ex.Message);
        }
    }

    private sealed record ProbeRow(string ServerName, string DatabaseName, string LoginName, string ProductVersion);
}
