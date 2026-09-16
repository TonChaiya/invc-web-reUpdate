namespace Invc.Infrastructure.Data;

/// <summary>
/// Configuration for the production INV database. Bound from the "InvDatabase" section.
/// The connection string must never be committed with credentials; use environment variables,
/// user secrets, or an untracked appsettings.*.local.json (see docs/development-setup.md).
/// </summary>
public sealed class InvDatabaseOptions
{
    public const string SectionName = "InvDatabase";

    /// <summary>Base connection string (server, database, authentication). Required.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Command timeout in seconds for report queries.</summary>
    public int CommandTimeoutSeconds { get; set; } = 30;
}
