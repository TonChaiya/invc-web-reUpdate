using System.ComponentModel.DataAnnotations;
using Invc.Core.Borrow;
using Invc.Web.Borrow;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Invc.Web.Pages.Borrow;

/// <summary>
/// ย้อนกลับรายการคืน — records a CORRECTION event against one RETURN event (never edits or deletes the original).
/// Quantity ≤ the event's remaining reversible quantity, reason required, actor/time from the server.
/// </summary>
public class CorrectModel(BorrowScreenService screens, IBorrowReturnRepository returns, TimeProvider clock, ILogger<CorrectModel> logger) : PageModel
{
    public sealed class Input
    {
        [Range(1, 99_999_999)] public decimal Quantity { get; set; }
        [Required(ErrorMessage = "ต้องระบุเหตุผลในการแก้ไข"), MaxLength(BorrowReturnRules.MaxNoteLength)] public string? Note { get; set; }
        [Required, StringLength(36, MinimumLength = 36)] public string ClientRequestId { get; set; } = string.Empty;
    }

    [BindProperty] public Input Form { get; set; } = new();
    public long EventId { get; private set; }
    public BorrowReturnEvent? Event { get; private set; }
    public string? Error { get; private set; }
    public bool StoreUnavailable { get; private set; }

    public async Task<IActionResult> OnGetAsync(long eventId, CancellationToken ct)
    {
        EventId = eventId;
        if (!await LoadAsync(eventId, ct)) return StoreUnavailable ? Page() : NotFound();
        Form = new Input { Quantity = Event!.ReversibleQty, ClientRequestId = Guid.NewGuid().ToString("D") };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(long eventId, CancellationToken ct)
    {
        EventId = eventId;
        if (!await LoadAsync(eventId, ct)) return StoreUnavailable ? Page() : NotFound();
        if (!ModelState.IsValid) { Error = string.IsNullOrWhiteSpace(Form.Note) ? "ต้องระบุเหตุผลในการแก้ไข" : "ข้อมูลไม่ถูกต้อง"; return Page(); }
        BorrowWriteResult result;
        try { result = await returns.RecordCorrectionAsync(new BorrowCorrectionRequest(eventId, Form.Quantity, clock.GetLocalNow().DateTime, screens.CurrentActor(), Form.Note!, Form.ClientRequestId), ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Borrow correction write failed (event {Event})", eventId);
            Error = BorrowScreenService.StoreError; return Page();
        }
        if (!result.Succeeded) { Error = result.Error; return Page(); }
        TempData["Notice"] = result.WasDuplicate ? "การแก้ไขนี้ถูกบันทึกไว้แล้ว (ไม่บันทึกซ้ำ)" : $"ย้อนกลับรายการคืน #{eventId} จำนวน {Form.Quantity:N0} เรียบร้อย";
        return RedirectToPage("/Borrow/History", new { receiveNo = Event!.ReceiveNo });
    }

    private async Task<bool> LoadAsync(long eventId, CancellationToken ct)
    {
        try { Event = await returns.GetEventAsync(eventId, ct); return Event is not null; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Borrow store read failed (correct page, event {Event})", eventId);
            StoreUnavailable = true; Error = BorrowScreenService.StoreError; return false;
        }
    }
}
