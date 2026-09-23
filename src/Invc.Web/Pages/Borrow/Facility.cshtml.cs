using System.ComponentModel.DataAnnotations;
using Invc.Core.Borrow;
using Invc.Core.Inventory;
using Invc.Web.Borrow;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Invc.Web.Pages.Borrow;

/// <summary>
/// Bills of one facility (newest first) with per-item borrowed / returned / outstanding, statuses, return actions and the
/// read-only "มีการรับเข้าหลังยืม" hints. Route: /Borrow/Facility/{facilityCode}. Reconciles first, like the index.
/// Returns can be recorded inline (handler <c>Return</c>): with JavaScript the page body is re-rendered in place
/// (no navigation), without JavaScript the same POST falls back to Post/Redirect/Get. The write itself always goes
/// through <see cref="IBorrowReturnRepository.RecordReturnAsync"/> — same validation, transaction, actor and audit trail
/// as the full /Borrow/Return page.
/// </summary>
public class FacilityModel(BorrowScreenService screens, IBorrowReturnRepository returns, TimeProvider clock, ILogger<FacilityModel> logger) : PageModel
{
    public sealed class QuickReturnInput
    {
        public int ItemRecordNumber { get; set; }
        public int BillRecordNumber { get; set; }
        [Range(1, 99_999_999)] public decimal Quantity { get; set; }
        [Required, StringLength(36, MinimumLength = 36)] public string ClientRequestId { get; set; } = string.Empty;
    }

    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Keyword { get; set; }

    [BindProperty(SupportsGet = true, Name = "status")]
    public string? StatusQuery { get; set; }

    [BindProperty] public QuickReturnInput Quick { get; set; } = new();

    public string FacilityCode { get; private set; } = string.Empty;
    public string? FacilityName { get; private set; }
    public BorrowStatusFilter Filter { get; private set; } = BorrowStatusFilter.All;
    /// <summary>All bills of the facility (unfiltered) — header totals are always the full picture.</summary>
    public BorrowFacilityView Facility { get; private set; } = new(string.Empty, null, []);
    public IReadOnlyList<BorrowBillView> Bills { get; private set; } = [];
    public BorrowSyncResult? Sync { get; private set; }
    public string? SyncError { get; private set; }
    public string? AdvisoryWarning { get; private set; }
    public bool HasError { get; private set; }
    public string? ErrorMessage { get; private set; }
    [TempData] public string? Notice { get; set; }
    [TempData] public string? Problem { get; set; }
    /// <summary>Result of an inline return, rendered inside the swapped body (never via TempData, so no navigation is needed).</summary>
    public string? InlineNotice { get; private set; }
    public string? InlineError { get; private set; }
    /// <summary>Source item of the last inline return — the row is highlighted after the swap.</summary>
    public int? UpdatedItemRecordNumber { get; private set; }

    public int ShownItemCount => Bills.Sum(b => b.Items.Count);
    public int CountFor(BorrowStatusFilter f) => Facility.Bills.Count(b => BorrowWorkboard.Matches(f, b.Status, b.HasConflict));

    public async Task<IActionResult> OnGetAsync(string? facilityCode, CancellationToken cancellationToken)
    {
        if (!Prepare(facilityCode)) return NotFound();
        var s = await screens.TrySyncAsync(cancellationToken);
        Sync = s.Result; SyncError = s.Warning;
        return await LoadAsync(cancellationToken) ? Page() : (HasError ? Page() : NotFound());
    }

    /// <summary>
    /// Inline "บันทึกคืน" from an item row. Quantity and identity are re-read and validated on the server; the actor and
    /// the event time come from the server, never from the form. AJAX requests get the re-rendered body partial, plain
    /// form posts get a redirect back to the same filtered view (PRG).
    /// </summary>
    public async Task<IActionResult> OnPostReturnAsync(string? facilityCode, CancellationToken cancellationToken)
    {
        if (!Prepare(facilityCode)) return NotFound();

        if (!ModelState.IsValid)
        {
            InlineError = "ข้อมูลไม่ถูกต้อง กรุณาตรวจสอบจำนวนที่คืน";
        }
        else
        {
            try
            {
                var request = new BorrowReturnRequest(Quick.ItemRecordNumber, Quick.BillRecordNumber, Quick.Quantity,
                    clock.GetLocalNow().DateTime, screens.CurrentActor(), null, Quick.ClientRequestId);
                var result = await returns.RecordReturnAsync(request, cancellationToken);
                if (result.Succeeded)
                {
                    UpdatedItemRecordNumber = Quick.ItemRecordNumber;
                    InlineNotice = result.WasDuplicate
                        ? "รายการคืนนี้ถูกบันทึกไว้แล้ว (ไม่บันทึกซ้ำ)"
                        : $"บันทึกคืน {Quick.Quantity:N0} เรียบร้อย";
                }
                else
                {
                    InlineError = result.Error;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Inline borrow return failed (item {Item})", Quick.ItemRecordNumber);
                InlineError = BorrowScreenService.StoreError;
            }
        }

        if (!await LoadAsync(cancellationToken) && !HasError) return NotFound();

        if (IsAjax)
        {
            return Partial("_FacilityBody", this);
        }
        if (InlineNotice is not null) Notice = InlineNotice;
        if (InlineError is not null) Problem = InlineError;
        return RedirectToPage("/Borrow/Facility", null,
            new { facilityCode = FacilityCode, status = Filter.ToQueryValue(), q = Keyword }, $"item-{Quick.ItemRecordNumber}");
    }

    /// <summary>
    /// Inline "คืนครบทั้งบิล" from a bill header. Confirmed in the browser (or on the /Borrow/ReturnBill page without
    /// JavaScript); the write is the same single transaction that returns the CURRENT outstanding quantity of every open
    /// item of the bill — any item failing validation rolls the whole bill back.
    /// </summary>
    public async Task<IActionResult> OnPostReturnBillAsync(string? facilityCode, int billRecordNumber, string? clientRequestId, CancellationToken cancellationToken)
    {
        if (!Prepare(facilityCode)) return NotFound();
        try
        {
            var result = await returns.RecordBillReturnAsync(billRecordNumber, clock.GetLocalNow().DateTime, screens.CurrentActor(), null, clientRequestId, cancellationToken);
            if (result.Succeeded)
            {
                InlineNotice = result.WasDuplicate
                    ? "การคืนทั้งบิลนี้ถูกบันทึกไว้แล้ว (ไม่บันทึกซ้ำ)"
                    : $"บันทึกคืนครบทั้งบิล ({result.EventIds.Count:N0} รายการ) เรียบร้อย";
            }
            else
            {
                InlineError = result.Error;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Inline borrow bill return failed (bill {Bill})", billRecordNumber);
            InlineError = BorrowScreenService.StoreError;
        }

        if (!await LoadAsync(cancellationToken) && !HasError) return NotFound();
        if (IsAjax) return Partial("_FacilityBody", this);
        if (InlineNotice is not null) Notice = InlineNotice;
        if (InlineError is not null) Problem = InlineError;
        return RedirectToPage("/Borrow/Facility", null,
            new { facilityCode = FacilityCode, status = Filter.ToQueryValue(), q = Keyword }, $"bill-{billRecordNumber}");
    }

    private bool IsAjax => string.Equals(Request.Headers["X-Requested-With"], "fetch", StringComparison.OrdinalIgnoreCase);

    private bool Prepare(string? facilityCode)
    {
        FacilityCode = facilityCode?.Trim() ?? string.Empty;
        Keyword = SearchKeyword.Normalize(Keyword);
        Filter = string.IsNullOrEmpty(StatusQuery) ? BorrowStatusFilter.All : BorrowStatusFilterExtensions.Parse(StatusQuery);
        return BorrowRules.IsValidFacilityCode(FacilityCode);
    }

    /// <summary>Re-reads the mirror + balances (+ advisory) and rebuilds the view. False = facility not found or store unreadable.</summary>
    private async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        var data = await screens.LoadFacilityAsync(FacilityCode, withHints: true, cancellationToken);
        if (data.Error is not null) { HasError = true; ErrorMessage = data.Error; return false; }
        AdvisoryWarning = data.AdvisoryWarning;
        if (data.Bills.Count == 0) return false;

        var name = data.Bills.Select(b => b.Source.FacilityName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));
        Facility = new BorrowFacilityView(FacilityCode, name, data.Bills.OrderByDescending(b => b.Source.DateReceive).ThenByDescending(b => b.Source.SourceRecordNumber).ToList());
        FacilityName = name;
        Bills = Apply(Facility.Bills, Keyword, Filter);
        return true;
    }

    /// <summary>
    /// Status filter applies per item (a bill stays when at least one item matches); keyword: bills whose receive/invoice no.
    /// matches keep all their (status-filtered) items, otherwise only items matching drug / code / lot are kept.
    /// </summary>
    internal static IReadOnlyList<BorrowBillView> Apply(IReadOnlyList<BorrowBillView> bills, string? keyword, BorrowStatusFilter status)
    {
        bool Has(string? s) => keyword is not null && s is not null && s.Contains(keyword, StringComparison.OrdinalIgnoreCase);
        var result = new List<BorrowBillView>();
        foreach (var b in bills)
        {
            var items = b.Items.Where(i => BorrowWorkboard.Matches(status, i.Status, i.HasConflict)).ToList();
            if (keyword is not null && !(Has(b.Source.ReceiveNo) || Has(b.Source.InvoiceNo)))
            {
                items = items.Where(i => Has(i.Source.DrugName) || Has(i.Source.WorkingCode) || Has(i.Source.LotNo)).ToList();
            }
            if (items.Count > 0) result.Add(b with { Items = items });
        }
        return result;
    }
}
