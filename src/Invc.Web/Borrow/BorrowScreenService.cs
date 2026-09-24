using Invc.Core.Borrow;

namespace Invc.Web.Borrow;

/// <summary>
/// Shared orchestration for the Borrow screens: tolerant reconciliation (INV failure ⇒ keep the mirror, warn), the
/// work-board read model (mirror + return balances, few set-based queries), the receipt-after-borrow advisory (INV,
/// SELECT only) and the server-side actor. Every MySQL failure is caught here and surfaced as <see cref="StoreError"/>
/// so the pages render their own error state instead of the global /Error page.
/// </summary>
public sealed class BorrowScreenService(BorrowSyncService sync, IBorrowMirrorRepository mirror, IBorrowReturnRepository returns,
                                        IBorrowReceiptAdvisoryRepository advisory, IHttpContextAccessor http, ExternalAccessMode access,
                                        ILogger<BorrowScreenService> logger)
{
    public const string StoreError = "ไม่สามารถอ่านข้อมูลยายืมจากฐานข้อมูลของเว็บได้ กรุณาตรวจสอบหน้า สถานะระบบ";
    public const string SyncWarning = "ไม่สามารถซิงก์ข้อมูลจาก INV ได้ในขณะนี้ — แสดงข้อมูลที่ซิงก์ไว้ล่าสุด";
    public const string AdvisoryWarning = "ไม่สามารถอ่านข้อมูลการรับเข้าหลังยืมจาก INV ได้ในขณะนี้";

    public sealed record SyncOutcome(BorrowSyncResult? Result, string? Warning);
    public sealed record BoardOutcome(BorrowWorkboard? Board, IReadOnlyList<BorrowBillView> Bills, string? Error, string? AdvisoryWarning);

    /// <summary>Full-snapshot reconciliation. INV read failure ⇒ mirror untouched + warning; MySQL failure ⇒ also a warning (the page then reports the store error on read).</summary>
    public async Task<SyncOutcome> TrySyncAsync(CancellationToken ct)
    {
        // External read-only hosting never writes to the application database — not even the mirror. It displays the
        // state that the internal, authenticated application synchronised.
        if (access.IsReadOnly) return new SyncOutcome(null, null);
        try { return new SyncOutcome(await sync.SyncAsync(ct), null); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Borrow reconciliation failed; showing the existing mirror");
            return new SyncOutcome(null, SyncWarning);
        }
    }

    /// <summary>Whole board (all facilities). Two MySQL queries: bills+items, balances.</summary>
    public async Task<BoardOutcome> LoadBoardAsync(CancellationToken ct)
    {
        try
        {
            var bills = await mirror.GetBillsAsync(null, ct);
            var balances = await returns.GetBalancesAsync(null, ct);
            var board = BorrowWorkboard.Build(bills, balances);
            return new BoardOutcome(board, board.Facilities.SelectMany(f => f.Bills).ToList(), null, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Borrow store read failed");
            return new BoardOutcome(null, [], StoreError, null);
        }
    }

    /// <summary>One facility's bills with balances and (INV, read-only) receipt-after-borrow hints for its outstanding items.</summary>
    public async Task<BoardOutcome> LoadFacilityAsync(string facilityCode, bool withHints, CancellationToken ct)
    {
        IReadOnlyList<BorrowSourceBill> bills; IReadOnlyList<BorrowItemBalance> balances;
        try
        {
            bills = await mirror.GetBillsAsync(facilityCode, ct);
            balances = await returns.GetBalancesAsync(facilityCode, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Borrow store read failed (facility {FacilityCode})", facilityCode);
            return new BoardOutcome(null, [], StoreError, null);
        }
        IReadOnlyList<BorrowReceiptHint>? hints = null; string? advisoryWarning = null;
        if (withHints && bills.Count > 0)
        {
            try
            {
                var open = BorrowWorkboard.BuildBills(bills, balances).SelectMany(b => b.Items).Where(i => i.OutstandingQty > 0m).ToList();
                if (open.Count > 0)
                {
                    var since = bills.Where(b => b.DateReceive is not null).Select(b => b.DateReceive!.Value).DefaultIfEmpty(DateTime.Today.AddYears(-5)).Min();
                    hints = await advisory.GetNonBorrowReceiptsAsync(open.Select(i => i.Source.WorkingCode).Distinct(StringComparer.OrdinalIgnoreCase).ToList(), since, ct);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Receipt-after-borrow advisory unavailable (facility {FacilityCode})", facilityCode);
                advisoryWarning = AdvisoryWarning;
            }
        }
        var views = BorrowWorkboard.BuildBills(bills, balances, hints);
        var facilities = BorrowWorkboard.Build(bills, balances).Facilities;
        return new BoardOutcome(new BorrowWorkboard(facilities), views, null, advisoryWarning);
    }

    /// <summary>Authenticated identity (IIS Windows auth) or the documented server-side fallback — never from the form.</summary>
    public string CurrentActor()
    {
        var ctx = http.HttpContext;
        return BorrowReturnRules.ResolveActor(ctx?.User?.Identity?.IsAuthenticated == true ? ctx.User.Identity.Name : null, ctx?.Connection.RemoteIpAddress?.ToString());
    }
}
