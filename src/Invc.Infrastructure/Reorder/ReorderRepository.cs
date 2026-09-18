using Dapper;
using Invc.Core.Inventory;
using Invc.Core.Reorder;
using Invc.Infrastructure.Data;
using Invc.Infrastructure.Inventory;

namespace Invc.Infrastructure.Reorder;

/// <summary>
/// SQL for the reorder recommendation report, ported from INV_Report_Purchase.asp / INV_Report_Purchase_Print.asp.
/// Reads raw threshold columns only; all classification is done in Core (<c>InventoryRules</c>), never in SQL,
/// so the formula cannot drift between two implementations.
///
/// Deliberate differences from the legacy pages (docs/phase3-reorder-parity.md):
///  * ordering uses the application's numeric-safe key (same as Inventory Status) instead of the legacy
///    <c>ORDER BY WORKING_CODE</c> text sort — identical for the all-digit, equal-length codes in production,
///    and row membership is unaffected;
///  * an optional keyword filter (same four columns and escaping as Inventory Status) — the legacy page had none.
/// </summary>
internal static class ReorderSql
{
    /// <summary>Legacy eligibility — reproduced verbatim, NOT the Inventory Status filter.</summary>
    public const string EligibilityFilter = """
        WHERE (m.NOUSE IS NULL OR m.NOUSE = '')
          AND (m.OUT_OF_LIST IS NULL OR m.OUT_OF_LIST = '')
        """;

    private const string SelectList = """
        SELECT m.WORKING_CODE   AS WorkingCode,
               m.DRUG_NAME      AS DrugName,
               m.QTY_ON_HAND    AS QtyOnHand,
               m.MIN_LEVEL      AS MinLevel,
               m.REORDER_QTY    AS ReorderQty,
               m.MAX_LEVEL      AS MaxLevel,
               m.SALE_UNIT      AS SaleUnit,
               m.LOCATION       AS Location,
               m.RATE_PER_MONTH AS RatePerMonth,
               RTRIM(m.ED_NED)  AS EdNedCode,
               e.EDNAME         AS EdNedName
        FROM dbo.INV_MD m
        OUTER APPLY (SELECT TOP 1 x.EDNAME FROM dbo.TBLED_NED x WHERE x.EDCODE = m.ED_NED ORDER BY x.EDCODE) e
        """;

    /// <summary>ประเภทเวชภัณฑ์ dropdown source (INV_MD.ED_NED → TBLED_NED). Read-only, five rows in production.</summary>
    public const string ItemTypes = """
        SELECT RTRIM(t.EDCODE) AS Code,
               RTRIM(t.EDNAME) AS Name,
               RTRIM(t.EDMAP)  AS ShortName
        FROM dbo.TBLED_NED t
        ORDER BY t.EDCODE
        """;

    private const string OrderBy = """
        ORDER BY CASE WHEN m.WORKING_CODE NOT LIKE '%[^0-9]%' THEN 0 ELSE 1 END,
                 CASE WHEN m.WORKING_CODE NOT LIKE '%[^0-9]%'
                      THEN RIGHT(REPLICATE('0', 20) + m.WORKING_CODE, 20)
                      ELSE m.WORKING_CODE END,
                 m.DRUG_NAME COLLATE Thai_CI_AS
        """;

    public const string EligibleAll = $"""
        {SelectList}
        {EligibilityFilter}
        {OrderBy}
        """;

    public const string EligibleSearch = $"""
        {SelectList}
        {EligibilityFilter}
          AND (   m.DRUG_NAME    LIKE @Pattern
               OR m.COMPOSITION  LIKE @Pattern
               OR m.HOSP_CODE    LIKE @Pattern
               OR m.WORKING_CODE LIKE @Pattern)
        {OrderBy}
        """;
}

/// <summary>Dapper implementation of <see cref="IReorderRepository"/> — one SELECT per request.</summary>
public sealed class ReorderRepository(ISqlConnectionFactory connections) : IReorderRepository
{
    public async Task<IReadOnlyList<ReorderItem>> GetEligibleAsync(string? keyword, CancellationToken cancellationToken = default)
    {
        var normalized = SearchKeyword.Normalize(keyword);
        await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);

        var command = normalized is null
            ? new CommandDefinition(ReadOnlySql.Ensure(ReorderSql.EligibleAll),
                commandTimeout: connections.CommandTimeoutSeconds, cancellationToken: cancellationToken)
            : new CommandDefinition(ReadOnlySql.Ensure(ReorderSql.EligibleSearch),
                new { Pattern = InventoryRepository.LikePattern(normalized) },
                commandTimeout: connections.CommandTimeoutSeconds, cancellationToken: cancellationToken);

        var rows = await connection.QueryAsync<ReorderItem>(command).ConfigureAwait(false);
        return rows.AsList();
    }

    public async Task<IReadOnlyList<ItemType>> GetItemTypesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<ItemType>(new CommandDefinition(ReadOnlySql.Ensure(ReorderSql.ItemTypes),
            commandTimeout: connections.CommandTimeoutSeconds, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.AsList();
    }
}
