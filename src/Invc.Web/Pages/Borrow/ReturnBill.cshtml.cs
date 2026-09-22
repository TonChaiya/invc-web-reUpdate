using System.ComponentModel.DataAnnotations;
using Invc.Core.Borrow;
using Invc.Web.Borrow;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Invc.Web.Pages.Borrow;

/// <summary>
/// คืนครบทั้งบิล — explicit confirmation page. POST creates one RETURN event per open item for the CURRENT outstanding
/// quantities (re-read inside one MySQL transaction); any item failing validation rolls the whole bill back.
/// </summary>
public class ReturnBillModel(BorrowScreenService screens, IBorrowReturnRepository returns, IBorrowMirrorRepository mirror, TimeProvider clock, ILogger<ReturnBillModel> logger) : PageModel
{
    public sealed class Input
    {
        public DateTime? EventAt { get; set; }
        [MaxLength(BorrowReturnRules.MaxNoteLength)] public string? Note { get; set; }
        [Required, StringLength(36, MinimumLength = 36)] public string ClientRequestId { get; set; } = string.Empty;
        /// <summary>Explicit confirmation checkbox — required.</summary>
        public bool Confirm { get; set; }
    }

    [BindProperty] public Input Form { get; set; } = new();
    public int BillRecordNumber { get; private set; }
    public BorrowBillView? Bill { get; private set; }
    public string? Error { get; private set; }
    public bool StoreUnavailable { get; private set; }
    public DateTime Now => clock.GetLocalNow().DateTime;
    public IReadOnlyList<BorrowItemView> OpenItems => Bill?.Items.Where(i => i.CanReturn).ToList() ?? [];

    public async Task<IActionResult> OnGetAsync(int billRecordNumber, CancellationToken ct)
    {
        BillRecordNumber = billRecordNumber;
        if (!await LoadAsync(billRecordNumber, ct)) return StoreUnavailable ? Page() : NotFound();
        Form = new Input { EventAt = Now, ClientRequestId = Guid.NewGuid().ToString("D") };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int billRecordNumber, CancellationToken ct)
    {
        BillRecordNumber = billRecordNumber;
        if (!await LoadAsync(billRecordNumber, ct)) return StoreUnavailable ? Page() : NotFound();
        if (!ModelState.IsValid) { Error = "ข้อมูลไม่ถูกต้อง"; return Page(); }
        if (!Form.Confirm) { Error = "กรุณายืนยันการคืนครบทั้งบิล"; return Page(); }
        var at = Form.EventAt ?? Now;
        if (at > Now.AddMinutes(5)) { Error = "วันที่คืนต้องไม่เป็นอนาคต"; return Page(); }
        BorrowWriteResult result;
        try { result = await returns.RecordBillReturnAsync(billRecordNumber, at, screens.CurrentActor(), Form.Note, Form.ClientRequestId, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Borrow bill return write failed (bill {Bill})", billRecordNumber);
            Error = BorrowScreenService.StoreError; return Page();
        }
        if (!result.Succeeded) { Error = result.Error; return Page(); }
        TempData["Notice"] = result.WasDuplicate ? "การคืนทั้งบิลนี้ถูกบันทึกไว้แล้ว (ไม่บันทึกซ้ำ)" : $"บันทึกคืนครบทั้งบิล {Bill!.Source.ReceiveNo} ({result.EventIds.Count:N0} รายการ) เรียบร้อย";
        return RedirectToPage("/Borrow/Facility", null, new { facilityCode = Bill!.Source.FacilityCode, status = "all" }, $"bill-{billRecordNumber}");
    }

    private async Task<bool> LoadAsync(int billRecordNumber, CancellationToken ct)
    {
        try
        {
            var bill = (await mirror.GetBillsAsync(null, ct)).FirstOrDefault(b => b.SourceRecordNumber == billRecordNumber);
            if (bill is null || !BorrowRules.IsValidFacilityCode(bill.FacilityCode)) return false;
            var balances = await returns.GetBalancesAsync(bill.FacilityCode, ct);
            Bill = BorrowWorkboard.BuildBills([bill], balances)[0];
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Borrow store read failed (return-bill page, bill {Bill})", billRecordNumber);
            StoreUnavailable = true; Error = BorrowScreenService.StoreError;
            return false;
        }
    }
}
