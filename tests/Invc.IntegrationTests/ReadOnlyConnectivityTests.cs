using Dapper;
using Invc.Infrastructure.Data;
using Invc.Infrastructure.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace Invc.IntegrationTests;

public class ReadOnlyConnectivityTests(ProductionReadOnlyFixture db) : IClassFixture<ProductionReadOnlyFixture>
{
    [SkippableFact]
    public async Task Select_1_succeeds()
    {
        var connections = db.RequireOrSkip();
        await using var c = await connections.OpenAsync();
        var one = await c.ExecuteScalarAsync<int>(ReadOnlySql.Ensure("SELECT 1"));
        Assert.Equal(1, one);
    }

    [SkippableFact]
    public async Task Connection_is_to_INV_and_not_sa()
    {
        var connections = db.RequireOrSkip();
        var health = await new DatabaseHealth(connections, NullLogger<DatabaseHealth>.Instance).CheckAsync();

        Assert.True(health.IsHealthy, health.Error);
        Assert.Equal("INV", health.DatabaseName);
        Assert.NotEqual("sa", health.LoginName, StringComparer.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public async Task Expected_legacy_tables_exist()
    {
        var connections = db.RequireOrSkip();
        await using var c = await connections.OpenAsync();
        string[] expected = ["INV_MD", "INV_MD_C", "VCAR", "BORROW", "MS_PO", "MS_PO_C", "MS_IVO", "SUBSTOCK", "MNTH_SUM", "MBS_RE_M"];
        var found = (await c.QueryAsync<string>(ReadOnlySql.Ensure(
            "SELECT t.name FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id WHERE s.name = 'dbo' AND t.name IN @Names"),
            new { Names = expected })).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.All(expected, n => Assert.Contains(n, found));
    }
}
