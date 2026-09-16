using Microsoft.Data.SqlClient;

namespace Invc.Infrastructure.Data;

/// <summary>
/// Fail-fast validation of deployment configuration. Checks configuration *shape* only — it never opens a
/// connection, so a temporary database outage cannot prevent the application from starting.
/// </summary>
public static class ProductionConfigurationValidator
{
    /// <summary>
    /// Returns the list of problems (empty when valid). In Production the caller should refuse to start when any exist.
    /// </summary>
    public static IReadOnlyList<string> Validate(string? connectionString, string? allowedHosts, bool isProduction)
    {
        var problems = new List<string>();

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            problems.Add("InvDatabase:ConnectionString is missing. Supply it through deployment configuration (e.g. environment variable InvDatabase__ConnectionString).");
        }
        else
        {
            try
            {
                var builder = new SqlConnectionStringBuilder(connectionString);
                if (string.Equals(builder.UserID, "sa", StringComparison.OrdinalIgnoreCase))
                {
                    problems.Add("InvDatabase:ConnectionString must not use the 'sa' login; use a dedicated read-only identity.");
                }
                if (string.IsNullOrWhiteSpace(builder.InitialCatalog))
                {
                    problems.Add("InvDatabase:ConnectionString must specify Initial Catalog / Database (INV).");
                }
                if (string.IsNullOrWhiteSpace(builder.DataSource))
                {
                    problems.Add("InvDatabase:ConnectionString must specify Data Source / Server.");
                }
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException or KeyNotFoundException)
            {
                problems.Add("InvDatabase:ConnectionString is not a valid SQL Server connection string.");
            }
        }

        if (isProduction)
        {
            var hosts = (allowedHosts ?? string.Empty).Trim();
            if (hosts.Length == 0 || hosts == "*" || hosts.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Contains("*"))
            {
                problems.Add("AllowedHosts must list the production host name(s) explicitly in Production (wildcard '*' is not accepted). Supply it through deployment configuration.");
            }
        }

        return problems;
    }
}
