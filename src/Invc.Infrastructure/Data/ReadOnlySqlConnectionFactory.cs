using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace Invc.Infrastructure.Data;

/// <summary>
/// Creates connections to the production INV database with read-only intent.
/// This is the only place a <see cref="SqlConnection"/> is constructed.
/// </summary>
public interface ISqlConnectionFactory
{
    /// <summary>Opens a new connection. Caller disposes.</summary>
    Task<SqlConnection> OpenAsync(CancellationToken cancellationToken = default);

    int CommandTimeoutSeconds { get; }
}

public sealed class ReadOnlySqlConnectionFactory : ISqlConnectionFactory
{
    private readonly string _connectionString;

    public ReadOnlySqlConnectionFactory(IOptions<InvDatabaseOptions> options)
    {
        var opts = options.Value;
        if (string.IsNullOrWhiteSpace(opts.ConnectionString))
        {
            throw new InvalidOperationException(
                $"Configuration '{InvDatabaseOptions.SectionName}:ConnectionString' is missing. " +
                "See docs/development-setup.md.");
        }

        _connectionString = BuildReadOnlyConnectionString(opts.ConnectionString);
        CommandTimeoutSeconds = opts.CommandTimeoutSeconds <= 0 ? 30 : opts.CommandTimeoutSeconds;
    }

    public int CommandTimeoutSeconds { get; }

    public async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqlConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Normalises the configured string and forces settings that document and enforce
    /// read-only use: ApplicationIntent=ReadOnly, no MARS, a descriptive application name,
    /// and a refusal to run as <c>sa</c>.
    /// </summary>
    internal static string BuildReadOnlyConnectionString(string configured)
    {
        var builder = new SqlConnectionStringBuilder(configured)
        {
            ApplicationIntent = ApplicationIntent.ReadOnly,
            MultipleActiveResultSets = false,
            ApplicationName = "INVC Web (read-only)",
        };

        if (string.Equals(builder.UserID, "sa", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The INVC web application must not connect as 'sa'. Configure a dedicated read-only login " +
                "or Windows authentication (see docs/phase0-security-and-data-access.md).");
        }

        if (string.IsNullOrWhiteSpace(builder.InitialCatalog))
        {
            throw new InvalidOperationException("The INV connection string must specify Initial Catalog / Database.");
        }

        return builder.ConnectionString;
    }
}
