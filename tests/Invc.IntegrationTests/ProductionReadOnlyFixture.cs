using Invc.Infrastructure.Data;
using Microsoft.Extensions.Options;

namespace Invc.IntegrationTests;

/// <summary>
/// Shared, read-only connection to the production INV database.
/// Rules (docs/development-setup.md): SELECT only, no fixtures, no seeding, no clean-up,
/// no transactions. When SQL Server is unreachable every test is skipped, never failed.
/// Connection string: environment variable INVC_TEST_CONNECTION, else Windows auth to the local instance.
/// </summary>
public sealed class ProductionReadOnlyFixture : IAsyncLifetime
{
    public const string DefaultConnectionString =
        "Server=.;Database=INV;Integrated Security=true;Encrypt=false;TrustServerCertificate=true;Connect Timeout=5";

    public ISqlConnectionFactory? Connections { get; private set; }
    public string? UnavailableReason { get; private set; }

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable("INVC_TEST_CONNECTION");
        var options = Options.Create(new InvDatabaseOptions
        {
            ConnectionString = string.IsNullOrWhiteSpace(configured) ? DefaultConnectionString : configured,
            CommandTimeoutSeconds = 30,
        });

        try
        {
            var factory = new ReadOnlySqlConnectionFactory(options);
            await using var probe = await factory.OpenAsync();
            Connections = factory;
        }
        catch (Exception ex)
        {
            UnavailableReason = $"INV database unavailable: {ex.GetType().Name}: {ex.Message}";
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public ISqlConnectionFactory RequireOrSkip()
    {
        Skip.If(Connections is null, UnavailableReason);
        return Connections!;
    }
}
