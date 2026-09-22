using System.ComponentModel.DataAnnotations;
using Invc.Core.Borrow;
using Invc.Web.Borrow;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Invc.Web.Pages.Borrow;

/// <summary>
/// บันทึกคืน (item level). GET shows ยืม / คืนแล้ว / คงเหลือ from the authoritative store; POST re-reads everything inside the
/// repository transaction (row lock), validates server-side, appends ONE RETURN event and redirects (PRG). The hidden
/// client request id makes a browser double-submit idempotent. Actor and time never come from the form.
/// </summary>
public class ReturnModel(BorrowScreenService screens, IBorrowReturnRepository returns, IBorrowMirrorRepository mirror, TimeProvider clock, ILogger<ReturnModel> logger) : PageModel
{
    public sealed class Input
    {
        [Range(1, 99_999_999)] public decimal Quantity { get; set; }
        /// <summary>Return date/time as entered (local). Default = now.</summary>
        public DateTime? EventAt { get; set; }
        [MaxLength(BorrowReturnRules.MaxNoteLength)] public string? Note { get; set; }
        /// <summary>Opaque per-form token generated on GET; stored with the event so a refresh/double-click cannot record twice.</summary>
        [Required, StringLength(36, MinimumLength = 36)] public string ClientRequestId { get; set; } = string.Empty;
    }

    [BindProperty] public Input Form { get; set; } = new();
    public int ItemRecordNumber { get; private set; }
    public BorrowBillView? Bill { get; private set; }
    public BorrowItemView? Item { get; private set; }
    public IReadOnlyList<BorrowReturnEvent> History { get; private set; } = [];
    public string? Error { get; private set; }
    public bool StoreUnavailable { get; private set; }
    public DateTime Now => clock.GetLocalNow().DateTime;

    public async Task<IActionResult> OnGetAsync(int itemRecordNumber, CancellationToken ct)
    {
        ItemRecordNumber = itemRecordNumber;
        if (!await LoadAsync(itemRecordNumber, ct)) return StoreUnavailable ? Page() : NotFound();
        Form = new Input { Quantity = Item!.OutstandingQty, EventAt = Now, ClientRequestId = Guid.NewGuid().ToString("D") };
        return Page();
    }

    public Task<IActionResult> OnPostAsync(int itemRecordNumber, CancellationToken ct) => SubmitAsync(itemRecordNumber, full: false, ct);

    /// <summary>"คืนครบรายการ": submits exactly the CURRENT outstanding quantity (re-read server-side) — still fully validated.</summary>
    public Task<IActionResult> OnPostFullAsync(int itemRecordNumber, CancellationToken ct) => SubmitAsync(itemRecordNumber, full: true, ct);

    private async Task<IActionResult> SubmitAsync(int itemRecordNumber, bool full, CancellationToken ct)
    {
        ItemRecordNumber = itemRecordNumber;
        if (!await LoadAsync(itemRecordNumber, ct)) return StoreUnavailable ? Page() : NotFound();
        if (full) { Form.Quantity = Item!.OutstandingQty; ModelState.Remove("Form.Quantity"); }
        if (!ModelState.IsValid) { Error = "ข้อมูลไม่ถูกต้อง กรุณาตรวจสอบจำนวนและวันที่"; return Page(); }
        var at = Form.EventAt ?? Now;
        if (at > Now.AddMinutes(5)) { Error = "วันที่คืนต้องไม่เป็นอนาคต"; return Page(); }
        var request = new BorrowReturnRequest(Item!.Source.SourceRecordNumber, Item.Source.SourceBillRecordNumber, Form.Quantity, at, screens.CurrentActor(), Form.Note, Form.ClientRequestId);
        BorrowWriteResult result;
        try { result = await returns.RecordReturnAsync(request, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Borrow return write failed (item {Item})", itemRecordNumber);
            Error = BorrowScreenService.StoreError; return Page();
        }
        if (!result.Succeeded) { Error = result.Error; return Page(); }
        TempData["Notice"] = result.WasDuplicate
            ? "รายการคืนนี้ถูกบันทึกไว้แล้ว (ไม่บันทึกซ้ำ)"
            : $"บันทึกคืน {Item.DisplayName} จำนวน {Form.Quantity:N0} เรียบร้อย";
        return RedirectToPage("/Borrow/Facility", null, new { facilityCode = Bill!.Source.FacilityCode, status = "all" }, $"item-{itemRecordNumber}");
    }

    private async Task<bool> LoadAsync(int itemRecordNumber, CancellationToken ct)
    {
        try
        {
            var bills = await mirror.GetBillsAsync(null, ct);
            var bill = bills.FirstOrDefault(b => b.Items.Any(i => i.SourceRecordNumber == itemRecordNumber));
            if (bill is null || !BorrowRules.IsValidFacilityCode(bill.FacilityCode)) return false;
            var balances = await returns.GetBalancesAsync(bill.FacilityCode, ct);
            Bill = BorrowWorkboard.BuildBills([bill], balances)[0];
            Item = Bill.Items.First(i => i.Source.SourceRecordNumber == itemRecordNumber);
            History = await returns.GetItemHistoryAsync(itemRecordNumber, ct);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Borrow store read failed (return page, item {Item})", itemRecordNumber);
            StoreUnavailable = true; Error = BorrowScreenService.StoreError;
            return false;
        }
    }
}
