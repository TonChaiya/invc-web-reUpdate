using System.Data;
using Dapper;
using Invc.Core.Borrow;
using Invc.Infrastructure.AppData;
using MySqlConnector;

namespace Invc.Infrastructure.Borrow;

/// <summary>
/// MySQL statements for the append-only return trail (`borrow_return_event`, db/mysql/002). Writes happen only inside a
/// transaction that first locks the mirrored item row (SELECT … FOR UPDATE), so two simultaneous returns of the same item
/// serialise and the second one sees the first one's quantity. Nothing is ever UPDATEd or DELETEd in this table.
/// </summary>
internal static class BorrowReturnSql
{
    public const string LockItem = """
        SELECT i.source_record_number AS SourceRecordNumber, i.source_bill_record_number AS SourceBillRecordNumber, i.receive_no AS ReceiveNo,
               i.working_code AS WorkingCode, i.drug_name AS DrugName, i.qty_order AS QtyOrder, b.facility_code AS FacilityCode, b.facility_name AS FacilityName
        FROM borrow_source_item i JOIN borrow_source_bill b ON b.source_record_number = i.source_bill_record_number
        WHERE i.source_record_number = @Id FOR UPDATE
        """;

    public const string LockBillItems = """
        SELECT i.source_record_number AS SourceRecordNumber, i.source_bill_record_number AS SourceBillRecordNumber, i.receive_no AS ReceiveNo,
               i.working_code AS WorkingCode, i.drug_name AS DrugName, i.qty_order AS QtyOrder, b.facility_code AS FacilityCode, b.facility_name AS FacilityName
        FROM borrow_source_item i JOIN borrow_source_bill b ON b.source_record_number = i.source_bill_record_number
        WHERE i.source_bill_record_number = @BillId ORDER BY i.source_record_number FOR UPDATE
        """;

    public const string NetReturned = "SELECT COALESCE(SUM(quantity_delta), 0) FROM borrow_return_event WHERE source_item_record_number = @Id";
    public const string ExistingByClientRequest = "SELECT id FROM borrow_return_event WHERE client_request_id = @ClientRequestId LIMIT 1";

    public const string Insert = """
        INSERT INTO borrow_return_event
            (source_item_record_number, source_bill_record_number, receive_no, working_code, drug_name, facility_code, facility_name,
             event_type, quantity_delta, event_at, actor, note, corrects_event_id, client_request_id, created_at)
        VALUES (@SourceItemRecordNumber, @SourceBillRecordNumber, @ReceiveNo, @WorkingCode, @DrugName, @FacilityCode, @FacilityName,
                @EventType, @QuantityDelta, @EventAt, @Actor, @Note, @CorrectsEventId, @ClientRequestId, @CreatedAt);
        SELECT LAST_INSERT_ID();
        """;

    public const string LockEvent = """
        SELECT e.id AS Id, e.source_item_record_number AS SourceItemRecordNumber, e.source_bill_record_number AS SourceBillRecordNumber, e.receive_no AS ReceiveNo,
               e.working_code AS WorkingCode, e.drug_name AS DrugName, e.facility_code AS FacilityCode, e.facility_name AS FacilityName, e.event_type AS EventTypeText,
               e.quantity_delta AS QuantityDelta, e.event_at AS EventAt, e.actor AS Actor, e.note AS Note, e.corrects_event_id AS CorrectsEventId, e.created_at AS CreatedAt,
               e.quantity_delta + COALESCE((SELECT SUM(c.quantity_delta) FROM borrow_return_event c WHERE c.corrects_event_id = e.id), 0) AS ReversibleQty
        FROM borrow_return_event e WHERE e.id = @Id FOR UPDATE
        """;

    public const string Balances = """
        SELECT e.source_item_record_number AS SourceItemRecordNumber, SUM(e.quantity_delta) AS ReturnedQty,
               MAX(CASE WHEN e.event_type = 'RETURN' THEN e.event_at END) AS LastReturnAt, COUNT(*) AS EventCount
        FROM borrow_return_event e
        WHERE (@FacilityCode IS NULL OR e.facility_code = @FacilityCode)
        GROUP BY e.source_item_record_number
        """;

    private const string EventSelect = """
        SELECT e.id AS Id, e.source_item_record_number AS SourceItemRecordNumber, e.source_bill_record_number AS SourceBillRecordNumber, e.receive_no AS ReceiveNo,
               e.working_code AS WorkingCode, e.drug_name AS DrugName, e.facility_code AS FacilityCode, e.facility_name AS FacilityName, e.event_type AS EventTypeText,
               e.quantity_delta AS QuantityDelta, e.event_at AS EventAt, e.actor AS Actor, e.note AS Note, e.corrects_event_id AS CorrectsEventId, e.created_at AS CreatedAt,
               e.quantity_delta + COALESCE((SELECT SUM(c.quantity_delta) FROM borrow_return_event c WHERE c.corrects_event_id = e.id), 0) AS ReversibleQty
        FROM borrow_return_event e
        """;

    public const string History = EventSelect + """

        WHERE (@FacilityCode IS NULL OR e.facility_code = @FacilityCode)
          AND (@ReceiveNo IS NULL OR e.receive_no = @ReceiveNo)
          AND (@Item IS NULL OR e.working_code LIKE @ItemLike OR e.drug_name LIKE @ItemLike)
          AND (@From IS NULL OR e.event_at >= @From)
          AND (@To IS NULL OR e.event_at < @To)
          AND (@Actor IS NULL OR e.actor LIKE @ActorLike)
        ORDER BY e.event_at DESC, e.id DESC
        LIMIT @Limit
        """;

    public const string ItemHistory = EventSelect + " WHERE e.source_item_record_number = @Id ORDER BY e.event_at DESC, e.id DESC";
    public const string ById = EventSelect + " WHERE e.id = @Id";
}

/// <summary>Dapper/MySqlConnector implementation of <see cref="IBorrowReturnRepository"/>.</summary>
public sealed class BorrowReturnRepository(IAppDbConnectionFactory connections, TimeProvider? clock = null) : IBorrowReturnRepository
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private int Timeout => connections.CommandTimeoutSeconds;

    public async Task<IReadOnlyList<BorrowItemBalance>> GetBalancesAsync(string? facilityCode, CancellationToken cancellationToken = default)
    {
        await using var c = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await c.QueryAsync<BalanceRow>(new CommandDefinition(BorrowReturnSql.Balances, new { FacilityCode = facilityCode }, commandTimeout: Timeout, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(r => new BorrowItemBalance(r.SourceItemRecordNumber, r.ReturnedQty, r.LastReturnAt, (int)r.EventCount)).ToList();
    }

    public async Task<BorrowWriteResult> RecordReturnAsync(BorrowReturnRequest request, CancellationToken cancellationToken = default)
    {
        await using var c = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = await c.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken).ConfigureAwait(false);
        try
        {
            if (await FindDuplicateAsync(c, tx, request.ClientRequestId, cancellationToken).ConfigureAwait(false) is { } dup) { await tx.CommitAsync(cancellationToken).ConfigureAwait(false); return BorrowWriteResult.Duplicate(dup); }
            var item = await c.QuerySingleOrDefaultAsync<LockedItem>(new CommandDefinition(BorrowReturnSql.LockItem, new { Id = request.SourceItemRecordNumber }, tx, Timeout, cancellationToken: cancellationToken)).ConfigureAwait(false);
            if (item is null) { await tx.RollbackAsync(CancellationToken.None).ConfigureAwait(false); return BorrowWriteResult.Fail("ไม่พบรายการยืมนี้ในข้อมูลที่ซิงก์ไว้"); }
            if (item.SourceBillRecordNumber != request.SourceBillRecordNumber) { await tx.RollbackAsync(CancellationToken.None).ConfigureAwait(false); return BorrowWriteResult.Fail("รายการไม่ตรงกับบิลที่ระบุ"); }
            var returned = await c.ExecuteScalarAsync<decimal>(new CommandDefinition(BorrowReturnSql.NetReturned, new { Id = item.SourceRecordNumber }, tx, Timeout, cancellationToken: cancellationToken)).ConfigureAwait(false);
            if (BorrowReturnRules.ValidateReturn(item.QtyOrder ?? 0m, returned, request.Quantity) is { } error) { await tx.RollbackAsync(CancellationToken.None).ConfigureAwait(false); return BorrowWriteResult.Fail(error); }
            var id = await InsertAsync(c, tx, item, BorrowReturnEventType.Return, request.Quantity, request.EventAt, request.Actor, request.Note, null, request.ClientRequestId, cancellationToken).ConfigureAwait(false);
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            return BorrowWriteResult.Ok(id);
        }
        catch { await tx.RollbackAsync(CancellationToken.None).ConfigureAwait(false); throw; }
    }

    public async Task<BorrowWriteResult> RecordBillReturnAsync(int sourceBillRecordNumber, DateTime eventAt, string actor, string? note, string? clientRequestId, CancellationToken cancellationToken = default)
    {
        await using var c = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = await c.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken).ConfigureAwait(false);
        try
        {
            if (await FindDuplicateAsync(c, tx, clientRequestId, cancellationToken).ConfigureAwait(false) is { } dup) { await tx.CommitAsync(cancellationToken).ConfigureAwait(false); return BorrowWriteResult.Duplicate(dup); }
            var items = (await c.QueryAsync<LockedItem>(new CommandDefinition(BorrowReturnSql.LockBillItems, new { BillId = sourceBillRecordNumber }, tx, Timeout, cancellationToken: cancellationToken)).ConfigureAwait(false)).AsList();
            if (items.Count == 0) { await tx.RollbackAsync(CancellationToken.None).ConfigureAwait(false); return BorrowWriteResult.Fail("ไม่พบบิลนี้ในข้อมูลที่ซิงก์ไว้"); }
            var ids = new List<long>(); var first = true;
            foreach (var item in items)
            {
                var returned = await c.ExecuteScalarAsync<decimal>(new CommandDefinition(BorrowReturnSql.NetReturned, new { Id = item.SourceRecordNumber }, tx, Timeout, cancellationToken: cancellationToken)).ConfigureAwait(false);
                var borrowed = item.QtyOrder ?? 0m;
                if (BorrowReturnRules.HasConflict(borrowed, returned)) { await tx.RollbackAsync(CancellationToken.None).ConfigureAwait(false); return BorrowWriteResult.Fail($"รายการ {item.WorkingCode} มีข้อมูลขัดแย้งกับต้นทาง ต้องตรวจสอบก่อน"); }
                var outstanding = BorrowReturnRules.Outstanding(borrowed, returned);
                if (outstanding <= 0m) continue;
                if (BorrowReturnRules.ValidateReturn(borrowed, returned, outstanding) is { } error) { await tx.RollbackAsync(CancellationToken.None).ConfigureAwait(false); return BorrowWriteResult.Fail($"รายการ {item.WorkingCode}: {error}"); }
                ids.Add(await InsertAsync(c, tx, item, BorrowReturnEventType.Return, outstanding, eventAt, actor, note, null, first ? clientRequestId : null, cancellationToken).ConfigureAwait(false));
                first = false;
            }
            if (ids.Count == 0) { await tx.RollbackAsync(CancellationToken.None).ConfigureAwait(false); return BorrowWriteResult.Fail("บิลนี้ไม่มีรายการคงค้าง"); }
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            return BorrowWriteResult.Ok([.. ids]);
        }
        catch { await tx.RollbackAsync(CancellationToken.None).ConfigureAwait(false); throw; }
    }

    public async Task<BorrowWriteResult> RecordCorrectionAsync(BorrowCorrectionRequest request, CancellationToken cancellationToken = default)
    {
        await using var c = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = await c.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken).ConfigureAwait(false);
        try
        {
            if (await FindDuplicateAsync(c, tx, request.ClientRequestId, cancellationToken).ConfigureAwait(false) is { } dup) { await tx.CommitAsync(cancellationToken).ConfigureAwait(false); return BorrowWriteResult.Duplicate(dup); }
            var ev = await c.QuerySingleOrDefaultAsync<EventRow>(new CommandDefinition(BorrowReturnSql.LockEvent, new { Id = request.CorrectsEventId }, tx, Timeout, cancellationToken: cancellationToken)).ConfigureAwait(false);
            if (ev is null) { await tx.RollbackAsync(CancellationToken.None).ConfigureAwait(false); return BorrowWriteResult.Fail("ไม่พบรายการคืนที่ต้องการแก้ไข"); }
            if (ev.EventTypeText != "RETURN") { await tx.RollbackAsync(CancellationToken.None).ConfigureAwait(false); return BorrowWriteResult.Fail("แก้ไขได้เฉพาะรายการคืน (ไม่ใช่รายการแก้ไข)"); }
            // lock the item row too so a concurrent RETURN cannot interleave with the reversal
            var item = await c.QuerySingleOrDefaultAsync<LockedItem>(new CommandDefinition(BorrowReturnSql.LockItem, new { Id = ev.SourceItemRecordNumber }, tx, Timeout, cancellationToken: cancellationToken)).ConfigureAwait(false);
            var netReturned = await c.ExecuteScalarAsync<decimal>(new CommandDefinition(BorrowReturnSql.NetReturned, new { Id = ev.SourceItemRecordNumber }, tx, Timeout, cancellationToken: cancellationToken)).ConfigureAwait(false);
            if (BorrowReturnRules.ValidateCorrection(request.Quantity, ev.ReversibleQty, netReturned, request.Note) is { } error) { await tx.RollbackAsync(CancellationToken.None).ConfigureAwait(false); return BorrowWriteResult.Fail(error); }
            var snapshot = item ?? new LockedItem   // source row may have disappeared from the mirror: keep the event's own snapshot
            {
                SourceRecordNumber = ev.SourceItemRecordNumber, SourceBillRecordNumber = ev.SourceBillRecordNumber, ReceiveNo = ev.ReceiveNo, WorkingCode = ev.WorkingCode,
                DrugName = ev.DrugName, FacilityCode = ev.FacilityCode, FacilityName = ev.FacilityName, QtyOrder = null,
            };
            var id = await InsertAsync(c, tx, snapshot, BorrowReturnEventType.Correction, -request.Quantity, request.EventAt, request.Actor, request.Note, ev.Id, request.ClientRequestId, cancellationToken).ConfigureAwait(false);
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            return BorrowWriteResult.Ok(id);
        }
        catch { await tx.RollbackAsync(CancellationToken.None).ConfigureAwait(false); throw; }
    }

    public async Task<IReadOnlyList<BorrowReturnEvent>> GetHistoryAsync(BorrowHistoryFilter f, CancellationToken cancellationToken = default)
    {
        await using var c = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var p = new
        {
            FacilityCode = Null(f.FacilityCode), ReceiveNo = Null(f.ReceiveNo), Item = Null(f.Item), ItemLike = Like(f.Item),
            From = f.From, To = f.To?.Date.AddDays(1), Actor = Null(f.Actor), ActorLike = Like(f.Actor), Limit = Math.Clamp(f.Limit, 1, 2000),
        };
        var rows = await c.QueryAsync<EventRow>(new CommandDefinition(BorrowReturnSql.History, p, commandTimeout: Timeout, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<BorrowReturnEvent>> GetItemHistoryAsync(int sourceItemRecordNumber, CancellationToken cancellationToken = default)
    {
        await using var c = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await c.QueryAsync<EventRow>(new CommandDefinition(BorrowReturnSql.ItemHistory, new { Id = sourceItemRecordNumber }, commandTimeout: Timeout, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(Map).ToList();
    }

    public async Task<BorrowReturnEvent?> GetEventAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var c = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var row = await c.QuerySingleOrDefaultAsync<EventRow>(new CommandDefinition(BorrowReturnSql.ById, new { Id = id }, commandTimeout: Timeout, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row is null ? null : Map(row);
    }

    private static string? Null(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    private static string? Like(string? s) => Null(s) is { } v ? "%" + v.Replace("%", "\\%").Replace("_", "\\_") + "%" : null;

    private async Task<long?> FindDuplicateAsync(MySqlConnection c, MySqlTransaction tx, string? clientRequestId, CancellationToken ct)
        => string.IsNullOrWhiteSpace(clientRequestId) ? null
           : await c.ExecuteScalarAsync<long?>(new CommandDefinition(BorrowReturnSql.ExistingByClientRequest, new { ClientRequestId = clientRequestId.Trim() }, tx, Timeout, cancellationToken: ct)).ConfigureAwait(false);

    private async Task<long> InsertAsync(MySqlConnection c, MySqlTransaction tx, LockedItem item, BorrowReturnEventType type, decimal delta, DateTime eventAt, string actor, string? note, long? corrects, string? clientRequestId, CancellationToken ct)
        => await c.ExecuteScalarAsync<long>(new CommandDefinition(BorrowReturnSql.Insert, new
        {
            SourceItemRecordNumber = item.SourceRecordNumber, item.SourceBillRecordNumber, item.ReceiveNo, item.WorkingCode, item.DrugName, item.FacilityCode, item.FacilityName,
            EventType = type == BorrowReturnEventType.Return ? "RETURN" : "CORRECTION", QuantityDelta = delta, EventAt = eventAt,
            Actor = actor, Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(), CorrectsEventId = corrects,
            ClientRequestId = string.IsNullOrWhiteSpace(clientRequestId) ? null : clientRequestId.Trim(), CreatedAt = _clock.GetLocalNow().DateTime,
        }, tx, Timeout, cancellationToken: ct)).ConfigureAwait(false);

    private static BorrowReturnEvent Map(EventRow r) => new()
    {
        Id = r.Id, SourceItemRecordNumber = r.SourceItemRecordNumber, SourceBillRecordNumber = r.SourceBillRecordNumber, ReceiveNo = r.ReceiveNo, WorkingCode = r.WorkingCode,
        DrugName = r.DrugName, FacilityCode = r.FacilityCode, FacilityName = r.FacilityName,
        EventType = r.EventTypeText == "CORRECTION" ? BorrowReturnEventType.Correction : BorrowReturnEventType.Return,
        QuantityDelta = r.QuantityDelta, EventAt = r.EventAt, Actor = r.Actor, Note = r.Note, CorrectsEventId = r.CorrectsEventId, CreatedAt = r.CreatedAt, ReversibleQty = r.ReversibleQty,
    };

    private sealed class BalanceRow
    {
        public int SourceItemRecordNumber { get; init; }
        public decimal ReturnedQty { get; init; }
        public DateTime? LastReturnAt { get; init; }
        public long EventCount { get; init; }
    }

    private sealed class LockedItem
    {
        public int SourceRecordNumber { get; init; }
        public int SourceBillRecordNumber { get; init; }
        public string ReceiveNo { get; init; } = string.Empty;
        public string WorkingCode { get; init; } = string.Empty;
        public string? DrugName { get; init; }
        public decimal? QtyOrder { get; init; }
        public string? FacilityCode { get; init; }
        public string? FacilityName { get; init; }
    }

    private sealed class EventRow
    {
        public long Id { get; init; }
        public int SourceItemRecordNumber { get; init; }
        public int SourceBillRecordNumber { get; init; }
        public string ReceiveNo { get; init; } = string.Empty;
        public string WorkingCode { get; init; } = string.Empty;
        public string? DrugName { get; init; }
        public string? FacilityCode { get; init; }
        public string? FacilityName { get; init; }
        public string EventTypeText { get; init; } = string.Empty;
        public decimal QuantityDelta { get; init; }
        public DateTime EventAt { get; init; }
        public string Actor { get; init; } = string.Empty;
        public string? Note { get; init; }
        public long? CorrectsEventId { get; init; }
        public DateTime CreatedAt { get; init; }
        public decimal ReversibleQty { get; init; }
    }
}
