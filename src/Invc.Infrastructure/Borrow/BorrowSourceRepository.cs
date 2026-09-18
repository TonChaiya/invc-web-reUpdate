using Dapper;
using Invc.Core.Borrow;
using Invc.Infrastructure.Data;

namespace Invc.Infrastructure.Borrow;

/// <summary>
/// SQL for the type-09 ("ยายืมจากหน่วยงานอื่น") snapshot from SQL Server INV. Two SELECTs, both through
/// <c>ReadOnlySql.Ensure</c>. Relationship proven on live data (docs/borrow-data-map.md): OTH_IVOC.RECEIVE_NO →
/// OTH_IVO.RECEIVE_NO is unique across all headers, every line has exactly one header. Facility and drug names use
/// OUTER APPLY TOP 1 because COMPANY.COMPANY_CODE is not unique (INV_MD.WORKING_CODE is, but the same shape is kept).
/// </summary>
internal static class BorrowSourceSql
{
    public const string Headers = """
        SELECT h.RECORD_NUMBER      AS SourceRecordNumber,
               RTRIM(h.RECEIVE_NO)  AS ReceiveNo,
               h.INVOICE_NO         AS InvoiceNo,
               h.INVOICE_DATE       AS InvoiceDate,
               h.DATE_RECEIVE       AS DateReceive,
               RTRIM(h.DPT_CODE)    AS FacilityCode,
               co.COMPANY_NAME      AS FacilityName,
               h.SYSDATE            AS SourceSysdate
        FROM dbo.OTH_IVO h
        OUTER APPLY (SELECT TOP 1 x.COMPANY_NAME FROM dbo.COMPANY x
                     WHERE x.COMPANY_CODE = h.DPT_CODE ORDER BY x.RECORD_NUMBER) co
        WHERE RTRIM(h.RCV_TYPE) = @TypeCode
        ORDER BY h.DATE_RECEIVE DESC, h.RECORD_NUMBER DESC
        """;

    public const string Lines = """
        SELECT c.RECORD_NUMBER      AS SourceRecordNumber,
               h.RECORD_NUMBER      AS SourceBillRecordNumber,
               RTRIM(c.RECEIVE_NO)  AS ReceiveNo,
               RTRIM(c.WORKING_CODE) AS WorkingCode,
               md.DRUG_NAME         AS DrugName,
               c.QTY_ORDER          AS QtyOrder,
               c.PACK_RATIO         AS PackRatio,
               c.LOTNO              AS LotNo,
               RTRIM(c.MANUFAC_CODE) AS ManufacCode,
               RTRIM(c.VENDORC)     AS VendorCode
        FROM dbo.OTH_IVOC c
        JOIN dbo.OTH_IVO h ON h.RECEIVE_NO = c.RECEIVE_NO
        OUTER APPLY (SELECT TOP 1 m.DRUG_NAME FROM dbo.INV_MD m
                     WHERE m.WORKING_CODE = c.WORKING_CODE ORDER BY m.RECORD_NUMBER) md
        WHERE RTRIM(h.RCV_TYPE) = @TypeCode
        ORDER BY h.RECORD_NUMBER, c.RECORD_NUMBER
        """;
}

/// <summary>Dapper implementation of <see cref="IBorrowSourceRepository"/> — SELECT only, INV is never written.</summary>
public sealed class BorrowSourceRepository(ISqlConnectionFactory connections) : IBorrowSourceRepository
{
    public async Task<IReadOnlyList<BorrowSourceBill>> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var parameters = new { TypeCode = new DbString { Value = BorrowRules.BorrowTypeCode, IsAnsi = false, Length = 4 } };
        await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);

        var headers = (await connection.QueryAsync<HeaderRow>(new CommandDefinition(
            ReadOnlySql.Ensure(BorrowSourceSql.Headers), parameters,
            commandTimeout: connections.CommandTimeoutSeconds, cancellationToken: cancellationToken)).ConfigureAwait(false)).AsList();
        var lines = (await connection.QueryAsync<BorrowSourceItem>(new CommandDefinition(
            ReadOnlySql.Ensure(BorrowSourceSql.Lines), parameters,
            commandTimeout: connections.CommandTimeoutSeconds, cancellationToken: cancellationToken)).ConfigureAwait(false)).AsList();

        var byBill = lines.GroupBy(l => l.SourceBillRecordNumber).ToDictionary(g => g.Key, g => (IReadOnlyList<BorrowSourceItem>)g.ToList());
        return headers.Select(h => new BorrowSourceBill
        {
            SourceRecordNumber = h.SourceRecordNumber,
            ReceiveNo = h.ReceiveNo,
            InvoiceNo = h.InvoiceNo,
            InvoiceDate = h.InvoiceDate,
            DateReceive = h.DateReceive,
            FacilityCode = h.FacilityCode,
            FacilityName = h.FacilityName,
            SourceSysdate = h.SourceSysdate,
            Items = byBill.TryGetValue(h.SourceRecordNumber, out var items) ? items : [],
        }).ToList();
    }

    private sealed class HeaderRow
    {
        public int SourceRecordNumber { get; init; }
        public string ReceiveNo { get; init; } = string.Empty;
        public string? InvoiceNo { get; init; }
        public DateTime? InvoiceDate { get; init; }
        public DateTime? DateReceive { get; init; }
        public string? FacilityCode { get; init; }
        public string? FacilityName { get; init; }
        public DateTime? SourceSysdate { get; init; }
    }
}
