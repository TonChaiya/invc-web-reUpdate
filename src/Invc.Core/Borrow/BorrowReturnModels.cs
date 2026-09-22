namespace Invc.Core.Borrow;

/// <summary>Kind of application-owned return event (append-only audit trail in MySQL `borrow_return_event`).</summary>
public enum BorrowReturnEventType
{
    /// <summary>Stock handed back to the lending facility — positive quantity.</summary>
    Return,
    /// <summary>Reversal of (part of) an earlier RETURN recorded by mistake — negative quantity, always references the event it corrects.</summary>
    Correction,
}

/// <summary>Derived per-item / per-bill / per-facility state. Never stored — always recomputed from BorrowedQty and the event trail.</summary>
public enum BorrowStatus
{
    /// <summary>Nothing returned yet (ReturnedQty = 0, BorrowedQty &gt; 0).</summary>
    Open,
    /// <summary>Some but not all returned.</summary>
    Partial,
    /// <summary>OutstandingQty = 0.</summary>
    Returned,
}

/// <summary>Status filter of the Borrow work screens: outstanding first (owner rule: primary = ค้างคืน / ต้องตรวจสอบ).</summary>
public enum BorrowStatusFilter { Outstanding, Partial, Returned, All }

public static class BorrowStatusFilterExtensions
{
    public static string ToQueryValue(this BorrowStatusFilter f) => f switch
    {
        BorrowStatusFilter.Outstanding => "outstanding",
        BorrowStatusFilter.Partial => "partial",
        BorrowStatusFilter.Returned => "returned",
        _ => "all",
    };

    public static BorrowStatusFilter Parse(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "partial" => BorrowStatusFilter.Partial,
        "returned" => BorrowStatusFilter.Returned,
        "all" => BorrowStatusFilter.All,
        _ => BorrowStatusFilter.Outstanding,
    };

    public static string ThaiLabel(this BorrowStatusFilter f) => f switch
    {
        BorrowStatusFilter.Outstanding => "คงค้าง",
        BorrowStatusFilter.Partial => "คืนบางส่วน",
        BorrowStatusFilter.Returned => "คืนครบ",
        _ => "ทั้งหมด",
    };

    public static string ThaiLabel(this BorrowStatus s) => s switch
    {
        BorrowStatus.Open => "ค้างคืน",
        BorrowStatus.Partial => "คืนบางส่วน",
        _ => "คืนครบ",
    };

    public static string ChipClass(this BorrowStatus s) => s switch
    {
        BorrowStatus.Open => "status-outstanding",
        BorrowStatus.Partial => "status-chip-warning",
        _ => "status-returned",
    };
}

/// <summary>
/// Business rules of the return workflow. Pure functions so the same rule is used by the page (display), by the
/// repository (inside the MySQL transaction) and by the tests.
/// BorrowedQty = source QTY_ORDER · ReturnedQty = Σ quantity_delta of the item's events (RETURN positive, CORRECTION negative)
/// · OutstandingQty = BorrowedQty − ReturnedQty. Invariant 0 ≤ ReturnedQty ≤ BorrowedQty is enforced at write time; a
/// violation can only arise when INV later lowers QTY_ORDER below what was already returned — that is reported as a
/// data-integrity conflict, never silently truncated or auto-corrected.
/// </summary>
public static class BorrowReturnRules
{
    public const int MaxNoteLength = 500;
    public const int MaxActorLength = 128;
    /// <summary>Server-side actor when the hosting model gives no authenticated identity (IIS anonymous, see docs/borrow-workflow.md).</summary>
    public const string AnonymousActorPrefix = "anonymous";

    public static decimal Outstanding(decimal borrowed, decimal returned) => Math.Max(0m, borrowed - returned);

    public static bool HasConflict(decimal borrowed, decimal returned) => returned > borrowed || returned < 0m;

    public static BorrowStatus Status(decimal borrowed, decimal returned)
    {
        if (returned <= 0m && borrowed > 0m) return BorrowStatus.Open;
        if (borrowed - returned > 0m) return BorrowStatus.Partial;
        return BorrowStatus.Returned;
    }

    /// <summary>Bill status from its items: OPEN when no item has any return, RETURNED when nothing is outstanding, else PARTIAL.</summary>
    public static BorrowStatus AggregateStatus(IEnumerable<(decimal Borrowed, decimal Returned)> items)
    {
        var any = false; var anyReturned = false; var anyOutstanding = false;
        foreach (var (b, r) in items)
        {
            any = true;
            if (r > 0m) anyReturned = true;
            if (Outstanding(b, r) > 0m) anyOutstanding = true;
        }
        if (!any) return BorrowStatus.Returned;
        if (!anyReturned && anyOutstanding) return BorrowStatus.Open;
        if (!anyOutstanding) return BorrowStatus.Returned;
        return BorrowStatus.Partial;
    }

    /// <summary>Returns the Thai validation message for a RETURN request, or null when the request is acceptable.</summary>
    public static string? ValidateReturn(decimal borrowed, decimal alreadyReturned, decimal requested)
    {
        if (requested <= 0m) return "จำนวนที่คืนต้องมากกว่า 0";
        if (requested != decimal.Truncate(requested)) return "จำนวนที่คืนต้องเป็นจำนวนเต็ม";
        var outstanding = Outstanding(borrowed, alreadyReturned);
        if (HasConflict(borrowed, alreadyReturned)) return "รายการนี้มีข้อมูลขัดแย้งกับต้นทาง (คืนแล้วมากกว่ายอดยืม) ต้องตรวจสอบก่อน";
        if (outstanding <= 0m) return "รายการนี้คืนครบแล้ว";
        if (requested > outstanding) return $"คืนได้ไม่เกินยอดคงเหลือ {outstanding:N0}";
        return null;
    }

    /// <summary>
    /// Validation for a CORRECTION of a RETURN event: <paramref name="reversible"/> = the event's quantity minus what has
    /// already been reversed against it; the item's net returned quantity must stay ≥ 0.
    /// </summary>
    public static string? ValidateCorrection(decimal requested, decimal reversible, decimal itemNetReturned, string? note)
    {
        if (string.IsNullOrWhiteSpace(note)) return "ต้องระบุเหตุผลในการแก้ไข";
        if (requested <= 0m) return "จำนวนที่ย้อนกลับต้องมากกว่า 0";
        if (requested != decimal.Truncate(requested)) return "จำนวนที่ย้อนกลับต้องเป็นจำนวนเต็ม";
        if (reversible <= 0m) return "รายการคืนนี้ถูกย้อนกลับครบแล้ว";
        if (requested > reversible) return $"ย้อนกลับได้ไม่เกิน {reversible:N0}";
        if (itemNetReturned - requested < 0m) return "ยอดคืนสุทธิของรายการจะติดลบ";
        return null;
    }

    /// <summary>Actor stored with every event: the authenticated name, else a clearly marked server-side fallback (never browser-supplied).</summary>
    public static string ResolveActor(string? authenticatedName, string? remoteAddress)
    {
        var name = string.IsNullOrWhiteSpace(authenticatedName) ? $"{AnonymousActorPrefix}:{(string.IsNullOrWhiteSpace(remoteAddress) ? "?" : remoteAddress.Trim())}" : authenticatedName.Trim();
        return name.Length <= MaxActorLength ? name : name[..MaxActorLength];
    }
}

/// <summary>One row of the append-only audit trail.</summary>
public sealed record BorrowReturnEvent
{
    public long Id { get; init; }
    public required int SourceItemRecordNumber { get; init; }
    public required int SourceBillRecordNumber { get; init; }
    public required string ReceiveNo { get; init; }
    public required string WorkingCode { get; init; }
    public string? DrugName { get; init; }
    public string? FacilityCode { get; init; }
    public string? FacilityName { get; init; }
    public required BorrowReturnEventType EventType { get; init; }
    /// <summary>Positive for RETURN, negative for CORRECTION.</summary>
    public required decimal QuantityDelta { get; init; }
    public required DateTime EventAt { get; init; }
    public required string Actor { get; init; }
    public string? Note { get; init; }
    public long? CorrectsEventId { get; init; }
    public DateTime CreatedAt { get; init; }
    /// <summary>For RETURN events: quantity still reversible (QuantityDelta + Σ corrections referencing this event).</summary>
    public decimal ReversibleQty { get; init; }
}

/// <summary>Net returned quantity per source item (Σ quantity_delta) plus the last return time — one set-based query.</summary>
public sealed record BorrowItemBalance(int SourceItemRecordNumber, decimal ReturnedQty, DateTime? LastReturnAt, int EventCount);

/// <summary>A RETURN request as validated and stored by the repository (actor/time come from the server, never the browser).</summary>
public sealed record BorrowReturnRequest(int SourceItemRecordNumber, int SourceBillRecordNumber, decimal Quantity, DateTime EventAt, string Actor, string? Note, string? ClientRequestId);

public sealed record BorrowCorrectionRequest(long CorrectsEventId, decimal Quantity, DateTime EventAt, string Actor, string Note, string? ClientRequestId);

/// <summary>Outcome of a write: either the new event id(s) or a business validation message (never an exception for expected rejections).</summary>
public sealed record BorrowWriteResult(bool Succeeded, string? Error, IReadOnlyList<long> EventIds)
{
    public static BorrowWriteResult Ok(params long[] ids) => new(true, null, ids);
    public static BorrowWriteResult Fail(string error) => new(false, error, []);
    /// <summary>The same client request id was already recorded (browser double-submit) — treated as success without a second write.</summary>
    public static BorrowWriteResult Duplicate(long existingId) => new(true, null, [existingId]) { WasDuplicate = true };
    public bool WasDuplicate { get; init; }
}

public sealed record BorrowHistoryFilter(string? FacilityCode, string? ReceiveNo, string? Item, DateTime? From, DateTime? To, string? Actor, int Limit = 500);

/// <summary>Application-owned return workflow store (MySQL). Writes are transactional with row locks; history is never deleted.</summary>
public interface IBorrowReturnRepository
{
    Task<IReadOnlyList<BorrowItemBalance>> GetBalancesAsync(string? facilityCode, CancellationToken cancellationToken = default);
    Task<BorrowWriteResult> RecordReturnAsync(BorrowReturnRequest request, CancellationToken cancellationToken = default);
    /// <summary>One transaction: a RETURN of the CURRENT outstanding quantity for every open item of the bill; any failure rolls all back.</summary>
    Task<BorrowWriteResult> RecordBillReturnAsync(int sourceBillRecordNumber, DateTime eventAt, string actor, string? note, string? clientRequestId, CancellationToken cancellationToken = default);
    Task<BorrowWriteResult> RecordCorrectionAsync(BorrowCorrectionRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BorrowReturnEvent>> GetHistoryAsync(BorrowHistoryFilter filter, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BorrowReturnEvent>> GetItemHistoryAsync(int sourceItemRecordNumber, CancellationToken cancellationToken = default);
    Task<BorrowReturnEvent?> GetEventAsync(long id, CancellationToken cancellationToken = default);
}

/// <summary>A NON-09 receipt line of the same drug received on/after the borrow date — advisory only ("มีการรับเข้าหลังยืม").</summary>
public sealed record BorrowReceiptHint(string ReceiveNo, string TypeCode, string? TypeName, DateTime? DateReceive, string WorkingCode, decimal? Qty, decimal? PackRatio, string? LotNo);

/// <summary>SQL Server INV, SELECT only: non-borrow receipt lines for a set of working codes since a date.</summary>
public interface IBorrowReceiptAdvisoryRepository
{
    Task<IReadOnlyList<BorrowReceiptHint>> GetNonBorrowReceiptsAsync(IReadOnlyCollection<string> workingCodes, DateTime since, CancellationToken cancellationToken = default);
}

// ---------------------------------------------------------------- read model ----------------------------------------------------------------

public sealed record BorrowItemView(BorrowSourceItem Source, decimal ReturnedQty, DateTime? LastReturnAt, int EventCount, IReadOnlyList<BorrowReceiptHint> ReceiptHints)
{
    public decimal BorrowedQty => Source.QtyOrder ?? 0m;
    public decimal OutstandingQty => BorrowReturnRules.Outstanding(BorrowedQty, ReturnedQty);
    public BorrowStatus Status => BorrowReturnRules.Status(BorrowedQty, ReturnedQty);
    /// <summary>INV lowered QTY_ORDER below what was already returned (or the trail went negative) — needs human review.</summary>
    public bool HasConflict => BorrowReturnRules.HasConflict(BorrowedQty, ReturnedQty);
    public bool CanReturn => !HasConflict && OutstandingQty > 0m;
    public string DisplayName => string.IsNullOrWhiteSpace(Source.DrugName) ? Source.WorkingCode : Source.DrugName!;
}

public sealed record BorrowBillView(BorrowSourceBill Source, IReadOnlyList<BorrowItemView> Items)
{
    public decimal BorrowedQty => Items.Sum(i => i.BorrowedQty);
    public decimal ReturnedQty => Items.Sum(i => i.ReturnedQty);
    public decimal OutstandingQty => Items.Sum(i => i.OutstandingQty);
    public BorrowStatus Status => BorrowReturnRules.AggregateStatus(Items.Select(i => (i.BorrowedQty, i.ReturnedQty)));
    public bool HasConflict => Items.Any(i => i.HasConflict);
    public bool CanReturnAll => Items.Any(i => i.CanReturn) && !HasConflict;
    public int ItemCount => Items.Count;
}

public sealed record BorrowFacilityView(string FacilityCode, string? FacilityName, IReadOnlyList<BorrowBillView> Bills)
{
    public string DisplayName => string.IsNullOrWhiteSpace(FacilityName) ? FacilityCode : FacilityName!;
    public int BillCount => Bills.Count;
    public int OpenBillCount => Bills.Count(b => b.Status != BorrowStatus.Returned);
    public int ItemCount => Bills.Sum(b => b.ItemCount);
    public decimal BorrowedQty => Bills.Sum(b => b.BorrowedQty);
    public decimal ReturnedQty => Bills.Sum(b => b.ReturnedQty);
    public decimal OutstandingQty => Bills.Sum(b => b.OutstandingQty);
    public DateTime? LatestDateReceive => Bills.Max(b => b.Source.DateReceive);
    public BorrowStatus Status => BorrowReturnRules.AggregateStatus(Bills.SelectMany(b => b.Items).Select(i => (i.BorrowedQty, i.ReturnedQty)));
    public bool HasConflict => Bills.Any(b => b.HasConflict);
}

/// <summary>The whole work board: facilities (outstanding first, then newest), with overall totals. Built in memory from the mirror + balances.</summary>
public sealed record BorrowWorkboard(IReadOnlyList<BorrowFacilityView> Facilities)
{
    public int FacilityCount => Facilities.Count;
    public int BillCount => Facilities.Sum(f => f.BillCount);
    public int OpenBillCount => Facilities.Sum(f => f.OpenBillCount);
    public int ItemCount => Facilities.Sum(f => f.ItemCount);
    public decimal BorrowedQty => Facilities.Sum(f => f.BorrowedQty);
    public decimal ReturnedQty => Facilities.Sum(f => f.ReturnedQty);
    public decimal OutstandingQty => Facilities.Sum(f => f.OutstandingQty);
    public int ConflictCount => Facilities.SelectMany(f => f.Bills).SelectMany(b => b.Items).Count(i => i.HasConflict);

    public static IReadOnlyList<BorrowBillView> BuildBills(IEnumerable<BorrowSourceBill> bills, IEnumerable<BorrowItemBalance> balances, IReadOnlyList<BorrowReceiptHint>? hints = null)
    {
        var byItem = balances.ToDictionary(b => b.SourceItemRecordNumber);
        var hintsByCode = (hints ?? []).GroupBy(h => h.WorkingCode.Trim(), StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        return bills.Select(bill => new BorrowBillView(bill, Inventory.DrugNameOrder.Sort(bill.Items, i => i.DrugName, i => i.WorkingCode)   // owner rule: A–Z by drug name
            .Select(item =>
            {
                byItem.TryGetValue(item.SourceRecordNumber, out var bal);
                var returned = bal?.ReturnedQty ?? 0m;
                var open = BorrowReturnRules.Outstanding(item.QtyOrder ?? 0m, returned) > 0m;
                IReadOnlyList<BorrowReceiptHint> itemHints = open && hintsByCode.TryGetValue(item.WorkingCode.Trim(), out var list)
                    ? list.Where(h => bill.DateReceive is null || h.DateReceive is null || h.DateReceive >= bill.DateReceive.Value.Date).OrderByDescending(h => h.DateReceive).ToList()
                    : [];
                return new BorrowItemView(item, returned, bal?.LastReturnAt, bal?.EventCount ?? 0, itemHints);
            }).ToList())).ToList();
    }

    public static BorrowWorkboard Build(IEnumerable<BorrowSourceBill> bills, IEnumerable<BorrowItemBalance> balances)
    {
        var billViews = BuildBills(bills, balances);
        var facilities = billViews
            .GroupBy(b => string.IsNullOrWhiteSpace(b.Source.FacilityCode) ? "-" : b.Source.FacilityCode!.Trim())
            .Select(g => new BorrowFacilityView(g.Key, g.Select(b => b.Source.FacilityName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)),
                g.OrderByDescending(b => b.Source.DateReceive).ThenByDescending(b => b.Source.SourceRecordNumber).ToList()))
            .OrderBy(f => f.OutstandingQty > 0m ? 0 : 1)
            .ThenByDescending(f => f.LatestDateReceive)
            .ThenBy(f => f.DisplayName, StringComparer.Ordinal)
            .ToList();
        return new BorrowWorkboard(facilities);
    }

    public static bool Matches(BorrowStatusFilter filter, BorrowStatus status, bool hasConflict) => filter switch
    {
        BorrowStatusFilter.Outstanding => hasConflict || status != BorrowStatus.Returned,
        BorrowStatusFilter.Partial => status == BorrowStatus.Partial,
        BorrowStatusFilter.Returned => status == BorrowStatus.Returned && !hasConflict,
        _ => true,
    };
}
