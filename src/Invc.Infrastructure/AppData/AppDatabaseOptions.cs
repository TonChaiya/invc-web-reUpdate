using MySqlConnector;

namespace Invc.Infrastructure.AppData;

/// <summary>
/// Configuration for the APPLICATION-OWNED MySQL database (`invc_web`) — the only writable store of INVC Web.
/// Bound from the "AppDatabase" section. Never commit credentials: supply <c>AppDatabase__ConnectionString</c> through
/// the environment / IIS, or an untracked <c>appsettings.{Environment}.local.json</c> for development.
/// This is a different provider, options class and factory from <see cref="Data.InvDatabaseOptions"/> on purpose.
/// </summary>
public sealed class AppDatabaseOptions
{
    public const string SectionName = "AppDatabase";

    /// <summary>MySqlConnector connection string (Server, Port, Database, User ID, Password …). Required for the Borrow module.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    public int CommandTimeoutSeconds { get; set; } = 30;
}

/// <summary>Opens connections to the application-owned MySQL database. Never to SQL Server INV.</summary>
public interface IAppDbConnectionFactory
{
    Task<MySqlConnection> OpenAsync(CancellationToken cancellationToken = default);
    int CommandTimeoutSeconds { get; }
    string DatabaseName { get; }
}

public sealed class MySqlAppDbConnectionFactory : IAppDbConnectionFactory
{
    private readonly string _connectionString;

    public MySqlAppDbConnectionFactory(Microsoft.Extensions.Options.IOptions<AppDatabaseOptions> options)
    {
        var opts = options.Value;
        if (string.IsNullOrWhiteSpace(opts.ConnectionString))
        {
            throw new InvalidOperationException($"Configuration '{AppDatabaseOptions.SectionName}:ConnectionString' is missing (application MySQL database).");
        }
        var builder = Validate(opts.ConnectionString);
        _connectionString = builder.ConnectionString;
        DatabaseName = builder.Database;
        CommandTimeoutSeconds = opts.CommandTimeoutSeconds <= 0 ? 30 : opts.CommandTimeoutSeconds;
    }

    public int CommandTimeoutSeconds { get; }
    public string DatabaseName { get; }

    public async Task<MySqlConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        var connection = new MySqlConnection(_connectionString);
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
    /// The application database must be a MySQL database that is not the INVC source. A connection string naming the
    /// database "INV", using SQL Server keywords (Initial Catalog / Integrated Security / Data Source / ApplicationIntent)
    /// or the login "sa" is refused so a misconfiguration can never point the writable side at SQL Server.
    /// </summary>
    internal static MySqlConnectionStringBuilder Validate(string configured)
    {
        foreach (var forbidden in new[] { "Initial Catalog", "Integrated Security", "Data Source", "ApplicationIntent", "Trusted_Connection" })
        {
            if (configured.Contains(forbidden, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"AppDatabase:ConnectionString looks like a SQL Server connection string ('{forbidden}'). The application database must be MySQL.");
            }
        }
        var builder = new MySqlConnectionStringBuilder(configured);
        if (string.IsNullOrWhiteSpace(builder.Database))
        {
            throw new InvalidOperationException("AppDatabase:ConnectionString must name the application database (Database=invc_web).");
        }
        if (string.Equals(builder.Database, "INV", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("AppDatabase:ConnectionString must not target the INV source database.");
        }
        if (string.Equals(builder.UserID, "sa", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("AppDatabase:ConnectionString must not use the 'sa' login.");
        }
        return builder;
    }
}
