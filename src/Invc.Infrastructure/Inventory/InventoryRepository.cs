using Dapper;
using Invc.Core.Inventory;
using Invc.Infrastructure.Data;

namespace Invc.Infrastructure.Inventory;

/// <summary>Dapper implementation of <see cref="IInventoryRepository"/> — SELECT only.</summary>
public sealed class InventoryRepository(ISqlConnectionFactory connections) : IInventoryRepository
{
    /// <summary>INV_MD.WORKING_CODE is nvarchar(7).</summary>
    public const int WorkingCodeMaxLength = 7;

    public async Task<IReadOnlyList<InventoryItem>> GetStatusAsync(string? keyword, CancellationToken cancellationToken = default)
    {
        var normalized = SearchKeyword.Normalize(keyword);
        await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);

        CommandDefinition command;
        if (normalized is null)
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
                new { Pattern = LikePattern(normalized) },
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
            ReadOnlySql.Ensure(InventorySql.SummaryByEdNed),
            commandTimeout: connections.CommandTimeoutSeconds,
            cancellationToken: cancellationToken);
        var groups = await connection.QueryAsync<EdNedGroup>(command).ConfigureAwait(false);
        return InventorySummary.FromGroups(groups.AsList());
    }

    public async Task<InventoryItemDetail?> GetDetailAsync(string workingCode, CancellationToken cancellationToken = default)
    {
        if (!IsValidWorkingCode(workingCode))
        {
            return null;
        }

        var parameter = new { WorkingCode = new DbString { Value = workingCode, IsAnsi = false, Length = WorkingCodeMaxLength } };
        await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);

        // Two SELECTs on one connection: header, then lots. Never one query per lot.
        var header = await connection.QuerySingleOrDefaultAsync<DetailHeaderRow>(new CommandDefinition(
            ReadOnlySql.Ensure(InventorySql.DetailHeader), parameter,
            commandTimeout: connections.CommandTimeoutSeconds, cancellationToken: cancellationToken)).ConfigureAwait(false);
        if (header is null)
        {
            return null;
        }

        var lots = await connection.QueryAsync<InventoryLot>(new CommandDefinition(
            ReadOnlySql.Ensure(InventorySql.DetailLots), parameter,
            commandTimeout: connections.CommandTimeoutSeconds, cancellationToken: cancellationToken)).ConfigureAwait(false);

        return new InventoryItemDetail
        {
            WorkingCode = header.WorkingCode,
            DrugName = header.DrugName,
            Composition = header.Composition,
            DosageForm = header.DosageForm,
            SaleUnit = header.SaleUnit,
            Location = header.Location,
            HospCode = header.HospCode,
            Ven = header.Ven,
            Abc = header.Abc,
            EdNedName = header.EdNedName,
            IsInactive = header.IsInactive,
            QtyOnHand = header.QtyOnHand,
            TotalValue = header.TotalValue,
            RatePerMonth = header.RatePerMonth,
            BorrowableQty = header.BorrowableQty,
            Lots = lots.AsList(),
        };
    }

    public async Task<IReadOnlyList<ItemLotTotals>> GetLotTotalsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<ItemLotTotals>(new CommandDefinition(
            ReadOnlySql.Ensure(InventorySql.LotTotals),
            commandTimeout: connections.CommandTimeoutSeconds, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.AsList();
    }

    /// <summary>Working codes are 1–7 characters, letters or digits (nvarchar(7); all-digit in production).</summary>
    public static bool IsValidWorkingCode(string? workingCode)
        => !string.IsNullOrEmpty(workingCode)
           && workingCode.Length <= WorkingCodeMaxLength
           && workingCode.All(char.IsLetterOrDigit);

    /// <summary>
    /// Builds the LIKE parameter for the search. The legacy page passed the raw keyword into LIKE, so
    /// '%', '_' and '[' acted as wildcards; here they are escaped so a literal search behaves literally.
    /// Parameter is nvarchar so Thai text matches under Thai_CI_AS.
    /// </summary>
    internal static DbString LikePattern(string keyword)
        => new() { Value = $"%{EscapeLike(keyword)}%", IsAnsi = false, Length = SearchKeyword.MaxLength * 3 + 2 };

    internal static string EscapeLike(string value)
        => value.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");

    /// <summary>Dapper row type for the detail header (settable properties so SQL types convert leniently).</summary>
    private sealed class DetailHeaderRow
    {
        public string WorkingCode { get; init; } = string.Empty;
        public string DrugName { get; init; } = string.Empty;
        public string? Composition { get; init; }
        public string? DosageForm { get; init; }
        public string? SaleUnit { get; init; }
        public string? Location { get; init; }
        public string? HospCode { get; init; }
        public string? Ven { get; init; }
        public string? Abc { get; init; }
        public string? EdNedName { get; init; }
        public bool IsInactive { get; init; }
        public decimal QtyOnHand { get; init; }
        public decimal TotalValue { get; init; }
        public decimal? RatePerMonth { get; init; }
        public decimal BorrowableQty { get; init; }
    }
}
