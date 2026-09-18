using Dapper;
using Invc.Core.Borrow;
using Invc.Infrastructure.AppData;
using MySqlConnector;

namespace Invc.Infrastructure.Borrow;

/// <summary>
/// MySQL statements for the application-owned mirror (`borrow_source_bill`, `borrow_source_item` — db/mysql/001_borrow_foundation.sql).
/// This is the ONLY place in the application that issues INSERT/UPDATE/DELETE, and it can only run against the
/// MySQL connection factory — never against SQL Server INV (different provider, options and factory).
/// </summary>
internal static class BorrowMirrorSql
{
    public const string SelectBillHashes = "SELECT source_record_number AS Id, source_hash AS Hash FROM borrow_source_bill";
    public const string SelectItemHashes = "SELECT source_record_number AS Id, source_hash AS Hash FROM borrow_source_item";

    public const string UpsertBill = """
        INSERT INTO borrow_source_bill
            (source_record_number, receive_no, invoice_no, invoice_date, date_receive, facility_code, facility_name, source_sysdate, source_hash, last_synced_at)
        VALUES (@SourceRecordNumber, @ReceiveNo, @InvoiceNo, @InvoiceDate, @DateReceive, @FacilityCode, @FacilityName, @SourceSysdate, @SourceHash, @LastSyncedAt)
        ON DUPLICATE KEY UPDATE
            receive_no = VALUES(receive_no), invoice_no = VALUES(invoice_no), invoice_date = VALUES(invoice_date),
            date_receive = VALUES(date_receive), facility_code = VALUES(facility_code), facility_name = VALUES(facility_name),
            source_sysdate = VALUES(source_sysdate), source_hash = VALUES(source_hash), last_synced_at = VALUES(last_synced_at)
        """;

    public const string UpsertItem = """
        INSERT INTO borrow_source_item
            (source_record_number, source_bill_record_number, receive_no, working_code, drug_name, qty_order, pack_ratio, lot_no, manufac_code, vendor_code, source_hash, last_synced_at)
        VALUES (@SourceRecordNumber, @SourceBillRecordNumber, @ReceiveNo, @WorkingCode, @DrugName, @QtyOrder, @PackRatio, @LotNo, @ManufacCode, @VendorCode, @SourceHash, @LastSyncedAt)
        ON DUPLICATE KEY UPDATE
            source_bill_record_number = VALUES(source_bill_record_number), receive_no = VALUES(receive_no), working_code = VALUES(working_code),
            drug_name = VALUES(drug_name), qty_order = VALUES(qty_order), pack_ratio = VALUES(pack_ratio), lot_no = VALUES(lot_no),
            manufac_code = VALUES(manufac_code), vendor_code = VALUES(vendor_code), source_hash = VALUES(source_hash), last_synced_at = VALUES(last_synced_at)
        """;

    public const string DeleteItems = "DELETE FROM borrow_source_item WHERE source_record_number IN @Ids";
    public const string DeleteBills = "DELETE FROM borrow_source_bill WHERE source_record_number IN @Ids";

    public const string SelectBills = """
        SELECT source_record_number AS SourceRecordNumber, receive_no AS ReceiveNo, invoice_no AS InvoiceNo, invoice_date AS InvoiceDate,
               date_receive AS DateReceive, facility_code AS FacilityCode, facility_name AS FacilityName, source_sysdate AS SourceSysdate
        FROM borrow_source_bill
        WHERE (@FacilityCode IS NULL OR facility_code = @FacilityCode)
        ORDER BY date_receive DESC, source_record_number DESC
        """;

    public const string SelectItems = """
        SELECT i.source_record_number AS SourceRecordNumber, i.source_bill_record_number AS SourceBillRecordNumber, i.receive_no AS ReceiveNo,
               i.working_code AS WorkingCode, i.drug_name AS DrugName, i.qty_order AS QtyOrder, i.pack_ratio AS PackRatio, i.lot_no AS LotNo,
               i.manufac_code AS ManufacCode, i.vendor_code AS VendorCode
        FROM borrow_source_item i
        JOIN borrow_source_bill b ON b.source_record_number = i.source_bill_record_number
        WHERE (@FacilityCode IS NULL OR b.facility_code = @FacilityCode)
        ORDER BY i.source_bill_record_number, i.source_record_number
        """;
}

/// <summary>Dapper/MySqlConnector implementation of <see cref="IBorrowMirrorRepository"/>.</summary>
public sealed class BorrowMirrorRepository(IAppDbConnectionFactory connections) : IBorrowMirrorRepository
{
    public async Task<BorrowMirrorState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var bills = await connection.QueryAsync<(int Id, string Hash)>(new CommandDefinition(BorrowMirrorSql.SelectBillHashes, commandTimeout: connections.CommandTimeoutSeconds, cancellationToken: cancellationToken)).ConfigureAwait(false);
        var items = await connection.QueryAsync<(int Id, string Hash)>(new CommandDefinition(BorrowMirrorSql.SelectItemHashes, commandTimeout: connections.CommandTimeoutSeconds, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return new BorrowMirrorState(bills.ToDictionary(r => r.Id, r => r.Hash), items.ToDictionary(r => r.Id, r => r.Hash));
    }

    public async Task ApplyAsync(BorrowReconciliationPlan plan, DateTime syncedAt, CancellationToken cancellationToken = default)
    {
        await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Order: bills first (items reference them), then items; deletions items-then-bills (FK cascade would also cover it).
            foreach (var bill in plan.BillsToInsert.Concat(plan.BillsToUpdate))
            {
                await connection.ExecuteAsync(new CommandDefinition(BorrowMirrorSql.UpsertBill, new
                {
                    bill.SourceRecordNumber, bill.ReceiveNo, bill.InvoiceNo, bill.InvoiceDate, bill.DateReceive, bill.FacilityCode, bill.FacilityName, bill.SourceSysdate,
                    SourceHash = bill.ComputeHash(), LastSyncedAt = syncedAt,
                }, tx, connections.CommandTimeoutSeconds, cancellationToken: cancellationToken)).ConfigureAwait(false);
            }
            foreach (var item in plan.ItemsToInsert.Concat(plan.ItemsToUpdate))
            {
                await connection.ExecuteAsync(new CommandDefinition(BorrowMirrorSql.UpsertItem, new
                {
                    item.SourceRecordNumber, item.SourceBillRecordNumber, item.ReceiveNo, item.WorkingCode, item.DrugName, item.QtyOrder, item.PackRatio, item.LotNo, item.ManufacCode, item.VendorCode,
                    SourceHash = item.ComputeHash(), LastSyncedAt = syncedAt,
                }, tx, connections.CommandTimeoutSeconds, cancellationToken: cancellationToken)).ConfigureAwait(false);
            }
            if (plan.ItemsToDelete.Count > 0)
            {
                await connection.ExecuteAsync(new CommandDefinition(BorrowMirrorSql.DeleteItems, new { Ids = plan.ItemsToDelete }, tx, connections.CommandTimeoutSeconds, cancellationToken: cancellationToken)).ConfigureAwait(false);
            }
            if (plan.BillsToDelete.Count > 0)
            {
                await connection.ExecuteAsync(new CommandDefinition(BorrowMirrorSql.DeleteBills, new { Ids = plan.BillsToDelete }, tx, connections.CommandTimeoutSeconds, cancellationToken: cancellationToken)).ConfigureAwait(false);
            }
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<IReadOnlyList<BorrowSourceBill>> GetBillsAsync(string? facilityCode, CancellationToken cancellationToken = default)
    {
        await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var p = new { FacilityCode = facilityCode };
        var bills = (await connection.QueryAsync<BillRow>(new CommandDefinition(BorrowMirrorSql.SelectBills, p, commandTimeout: connections.CommandTimeoutSeconds, cancellationToken: cancellationToken)).ConfigureAwait(false)).AsList();
        var items = (await connection.QueryAsync<BorrowSourceItem>(new CommandDefinition(BorrowMirrorSql.SelectItems, p, commandTimeout: connections.CommandTimeoutSeconds, cancellationToken: cancellationToken)).ConfigureAwait(false)).AsList();
        var byBill = items.GroupBy(i => i.SourceBillRecordNumber).ToDictionary(g => g.Key, g => (IReadOnlyList<BorrowSourceItem>)g.ToList());
        return bills.Select(b => new BorrowSourceBill
        {
            SourceRecordNumber = b.SourceRecordNumber, ReceiveNo = b.ReceiveNo, InvoiceNo = b.InvoiceNo, InvoiceDate = b.InvoiceDate, DateReceive = b.DateReceive,
            FacilityCode = b.FacilityCode, FacilityName = b.FacilityName, SourceSysdate = b.SourceSysdate,
            Items = byBill.TryGetValue(b.SourceRecordNumber, out var list) ? list : [],
        }).ToList();
    }

    private sealed class BillRow
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
