using Dapper;
using Invc.Core.Inventory;
using Invc.Infrastructure.Data;

namespace Invc.Infrastructure.Inventory;

/// <summary>Dapper implementation of <see cref="IInventoryRepository"/> — SELECT only.</summary>
public sealed class InventoryRepository(ISqlConnectionFactory connections) : IInventoryRepository
{
    public async Task<IReadOnlyList<InventoryItem>> GetStatusAsync(string? keyword, CancellationToken cancellationToken = default)
    {
        var trimmed = keyword?.Trim();
        await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);

        CommandDefinition command;
        if (string.IsNullOrEmpty(trimmed))
        {
            command = new CommandDefinition(
                ReadOnlySql.Ensure(InventorySql.StatusAll),
                commandTimeout: connections.CommandTimeoutSeconds,
                cancellationToken: cancellationToken);
        }
        else
        {
            command = new CommandDefinition(
                ReadOnlySql.Ensure(InventorySql.StatusSearch),
                new { Pattern = new DbString { Value = $"%{EscapeLike(trimmed)}%", IsAnsi = false, Length = 60 } },
                commandTimeout: connections.CommandTimeoutSeconds,
                cancellationToken: cancellationToken);
        }

        var rows = await connection.QueryAsync<InventoryItem>(command).ConfigureAwait(false);
        return rows.AsList();
    }

    public async Task<InventorySummary> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var command = new CommandDefinition(
            ReadOnlySql.Ensure(InventorySql.Summary),
            commandTimeout: connections.CommandTimeoutSeconds,
            cancellationToken: cancellationToken);
        return await connection.QuerySingleAsync<InventorySummary>(command).ConfigureAwait(false);
    }

    /// <summary>
    /// The legacy page passed the raw keyword into LIKE, so '%' and '_' acted as wildcards.
    /// We parameterise (no injection) and escape LIKE metacharacters so a literal search behaves literally.
    /// </summary>
    internal static string EscapeLike(string value)
        => value.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
}
