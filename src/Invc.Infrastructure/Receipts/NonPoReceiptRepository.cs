using Dapper;
using Invc.Core.Inventory;
using Invc.Core.Receipts;
using Invc.Infrastructure.Data;
using Invc.Infrastructure.Inventory;

namespace Invc.Infrastructure.Receipts;

/// <summary>
/// SQL for non-PO receipts (OTH_IVO / OTH_IVOC / RCV_TYPE). Evidence and key proofs: docs/phase5-receipts-data-map.md.
///  * header key OTH_IVO.RECEIVE_NO; lines join on OTH_IVOC.RECEIVE_NO (proven 1 018/1 018, 0 orphans);
///  * fiscal year = Thai FY of OTH_IVO.DATE_RECEIVE, applied as a typed [from, to) date-range parameter;
///  * RCV_TYPE and COMPANY (source) are LEFT JOIN / OUTER APPLY TOP 1 so a header is never lost or multiplied;
///  * line aggregates come from one grouped subquery (no N+1); item names via OUTER APPLY on the unique WORKING_CODE.
/// </summary>
internal static class NonPoReceiptSql
{
    private const string HeaderSelect = """
        SELECT h.RECEIVE_NO            AS ReceiveNo,
               h.INVOICE_NO            AS InvoiceNo,
               h.INVOICE_DATE          AS InvoiceDate,
               h.DATE_RECEIVE          AS DateReceive,
               RTRIM(h.RCV_TYPE)       AS TypeCode,
               t.RCV_NAME              AS TypeName,
               h.DPT_CODE              AS SourceCode,
               src.COMPANY_NAME        AS SourceName,
               h.TOTAL_ITEM            AS TotalItem,
               h.TOTAL_VALUE           AS TotalValue,
               h.OTH_NOTE              AS Note,
               ISNULL(l.LineCount, 0)  AS LineCount,
               ISNULL(l.LineQtySum, 0) AS LineQtySum
        FROM dbo.OTH_IVO h
        LEFT JOIN dbo.RCV_TYPE t ON t.RCV_TYPE_CODE = RTRIM(h.RCV_TYPE)
        OUTER APPLY (SELECT TOP 1 c.COMPANY_NAME FROM dbo.COMPANY c WHERE c.COMPANY_CODE = h.DPT_CODE ORDER BY c.RECORD_NUMBER) src
        LEFT JOIN (SELECT RECEIVE_NO, COUNT(*) AS LineCount, SUM(QTY_ORDER) AS LineQtySum
                   FROM dbo.OTH_IVOC GROUP BY RECEIVE_NO) l ON l.RECEIVE_NO = h.RECEIVE_NO
        """;

    /// <summary>Fiscal-year window on DATE_RECEIVE: @From inclusive, @To exclusive (typed datetime parameters).</summary>
    private const string PeriodFilter = "WHERE h.DATE_RECEIVE >= @From AND h.DATE_RECEIVE < @To";

    private const string HeaderOrder = "ORDER BY h.DATE_RECEIVE DESC, h.RECEIVE_NO DESC";

    public const string HeadersByPeriod = $"""
        {HeaderSelect}
        {PeriodFilter}
        {HeaderOrder}
        """;

    public const string HeadersByPeriodSearch = $"""
        {HeaderSelect}
        {PeriodFilter}
          AND (   h.RECEIVE_NO   LIKE @Pattern
               OR h.INVOICE_NO   LIKE @Pattern
               OR t.RCV_NAME     LIKE @Pattern
               OR src.COMPANY_NAME LIKE @Pattern
               OR h.DPT_CODE     LIKE @Pattern)
        {HeaderOrder}
        """;

    public const string HeaderByReceiveNo = $"""
        {HeaderSelect}
        WHERE h.RECEIVE_NO = @ReceiveNo
        ORDER BY h.RECORD_NUMBER
        """;

    public const string Lines = """
        SELECT c.WORKING_CODE  AS WorkingCode,
               d.DRUG_NAME     AS DrugName,
               c.QTY_ORDER     AS QtyOrder,
               c.PACK_RATIO    AS PackRatio,
               c.UNIT_VALUE    AS UnitValue,
               c.EXPIRED_DATE  AS ExpiredDate,
               c.LOTNO         AS LotNo,
               c.LOCATION      AS Location,
               c.MANUFAC_CODE  AS ManufacCode,
               c.VENDORC       AS VendorCode
        FROM dbo.OTH_IVOC c
        OUTER APPLY (SELECT TOP 1 m.DRUG_NAME FROM dbo.INV_MD m WHERE m.WORKING_CODE = c.WORKING_CODE ORDER BY m.RECORD_NUMBER) d
        WHERE c.RECEIVE_NO = @ReceiveNo
        ORDER BY c.RECORD_NUMBER
        """;

    public const string Types = """
        SELECT RTRIM(RCV_TYPE_CODE) AS Code, RCV_NAME AS Name FROM dbo.RCV_TYPE ORDER BY RCV_TYPE_CODE
        """;

    /// <summary>Distinct DATE_RECEIVE dates (small set) — fiscal years are derived in Core with the shared rule.</summary>
    public const string ReceiveDates = """
        SELECT DISTINCT CAST(DATE_RECEIVE AS date) AS D FROM dbo.OTH_IVO WHERE DATE_RECEIVE IS NOT NULL
        """;

    public static IEnumerable<string> AllStatements()
    {
        yield return HeadersByPeriod;
        yield return HeadersByPeriodSearch;
        yield return HeaderByReceiveNo;
        yield return Lines;
        yield return Types;
        yield return ReceiveDates;
    }
}

public sealed class NonPoReceiptRepository(ISqlConnectionFactory connections) : INonPoReceiptRepository
{
    public async Task<IReadOnlyList<int>> GetFiscalYearsWithDataAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var dates = await connection.QueryAsync<DateTime>(Cmd(NonPoReceiptSql.ReceiveDates, null, cancellationToken)).ConfigureAwait(false);
        return dates.Select(d => ReceiptRules.FiscalYearOf(d)!.Value).Distinct().OrderByDescending(y => y).ToList();
    }

    public async Task<IReadOnlyList<ReceiptTypeOption>> GetTypesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<ReceiptTypeOption>(Cmd(NonPoReceiptSql.Types, null, cancellationToken)).ConfigureAwait(false);
        return rows.AsList();
    }

    public async Task<IReadOnlyList<NonPoReceiptSummary>> GetHeadersAsync(int fiscalYear, string? keyword, CancellationToken cancellationToken = default)
    {
        var (from, to) = FiscalYearWindow(fiscalYear);
        var normalized = SearchKeyword.Normalize(keyword);
        await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);

        var command = normalized is null
            ? Cmd(NonPoReceiptSql.HeadersByPeriod, new { From = from, To = to }, cancellationToken)
            : Cmd(NonPoReceiptSql.HeadersByPeriodSearch, new { From = from, To = to, Pattern = InventoryRepository.LikePattern(normalized) }, cancellationToken);

        var rows = await connection.QueryAsync<NonPoReceiptSummary>(command).ConfigureAwait(false);
        return rows.AsList();
    }

    public async Task<NonPoReceiptDetail?> GetDetailAsync(string receiveNo, CancellationToken cancellationToken = default)
    {
        if (!ReceiptRules.IsValidReceiptNo(receiveNo))
        {
            return null;
        }

        var param = new { ReceiveNo = new DbString { Value = receiveNo, IsAnsi = false, Length = ReceiptRules.ReceiveNoMaxLength } };
        await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);

        var headers = (await connection.QueryAsync<NonPoReceiptSummary>(Cmd(NonPoReceiptSql.HeaderByReceiveNo, param, cancellationToken)).ConfigureAwait(false)).ToList();
        if (headers.Count == 0)
        {
            return null;
        }

        var lines = await connection.QueryAsync<NonPoReceiptLine>(Cmd(NonPoReceiptSql.Lines, param, cancellationToken)).ConfigureAwait(false);
        return new NonPoReceiptDetail { Header = headers[0], Lines = lines.AsList() };
    }

    /// <summary>Thai fiscal year N covers 1 Oct (N−1−543) … 30 Sep (N−543); returned as [from, to) datetimes.</summary>
    internal static (DateTime From, DateTime To) FiscalYearWindow(int buddhistFiscalYear)
    {
        var gregorianEnd = buddhistFiscalYear - Core.Common.ThaiFiscalYear.BuddhistEraOffset;
        return (new DateTime(gregorianEnd - 1, 10, 1), new DateTime(gregorianEnd, 10, 1));
    }

    private CommandDefinition Cmd(string sql, object? parameters, CancellationToken cancellationToken)
        => new(ReadOnlySql.Ensure(sql), parameters, commandTimeout: connections.CommandTimeoutSeconds, cancellationToken: cancellationToken);
}
