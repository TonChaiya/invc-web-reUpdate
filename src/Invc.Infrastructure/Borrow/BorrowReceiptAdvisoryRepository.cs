using Dapper;
using Invc.Core.Borrow;
using Invc.Infrastructure.Data;

namespace Invc.Infrastructure.Borrow;

/// <summary>
/// "มีการรับเข้าหลังยืม" advisory — SQL Server INV, SELECT only. One set-based query for all working codes of the
/// outstanding borrow items: every NON-09 receipt line (OTH_IVOC ⋈ OTH_IVO, RCV_TYPE ≠ '09') received on/after
/// <c>@Since</c>. The per-item date rule (receipt date ≥ that item's borrow date) is applied in memory by
/// <see cref="BorrowWorkboard.BuildBills"/>. Purely informational: nothing is ever marked returned from this.
/// </summary>
internal static class BorrowReceiptAdvisorySql
{
    public const string NonBorrowReceipts = """
        SELECT RTRIM(h.RECEIVE_NO)   AS ReceiveNo,
               RTRIM(h.RCV_TYPE)     AS TypeCode,
               t.RCV_NAME            AS TypeName,
               h.DATE_RECEIVE        AS DateReceive,
               RTRIM(c.WORKING_CODE) AS WorkingCode,
               c.QTY_ORDER           AS Qty,
               c.PACK_RATIO          AS PackRatio,
               c.LOTNO               AS LotNo
        FROM dbo.OTH_IVOC c
        JOIN dbo.OTH_IVO h ON h.RECEIVE_NO = c.RECEIVE_NO
        LEFT JOIN dbo.RCV_TYPE t ON t.RCV_TYPE_CODE = RTRIM(h.RCV_TYPE)
        WHERE RTRIM(h.RCV_TYPE) <> @BorrowType
          AND h.DATE_RECEIVE >= @Since
          AND RTRIM(c.WORKING_CODE) IN @WorkingCodes
        ORDER BY h.DATE_RECEIVE DESC, h.RECORD_NUMBER DESC, c.RECORD_NUMBER
        """;
}

public sealed class BorrowReceiptAdvisoryRepository(ISqlConnectionFactory connections) : IBorrowReceiptAdvisoryRepository
{
    public async Task<IReadOnlyList<BorrowReceiptHint>> GetNonBorrowReceiptsAsync(IReadOnlyCollection<string> workingCodes, DateTime since, CancellationToken cancellationToken = default)
    {
        if (workingCodes.Count == 0) return [];
        // Dapper expands @WorkingCodes into one parameter per code; SQL Server allows 2 100 parameters — chunk defensively.
        var result = new List<BorrowReceiptHint>();
        await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        foreach (var chunk in workingCodes.Select(w => w.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Chunk(1000))
        {
            var rows = await connection.QueryAsync<BorrowReceiptHint>(new CommandDefinition(
                ReadOnlySql.Ensure(BorrowReceiptAdvisorySql.NonBorrowReceipts),
                new { BorrowType = BorrowRules.BorrowTypeCode, Since = since.Date, WorkingCodes = chunk },
                commandTimeout: connections.CommandTimeoutSeconds, cancellationToken: cancellationToken)).ConfigureAwait(false);
            result.AddRange(rows);
        }
        return result;
    }
}
