using Invc.Core.Borrow;
using Invc.Core.Inventory;
using Invc.Web.Borrow;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Invc.Web.Pages.Borrow;

/// <summary>
/// Bills of one facility (newest first) with per-item borrowed / returned / outstanding, statuses, return actions and the
/// read-only "มีการรับเข้าหลังยืม" hints. Route: /Borrow/Facility/{facilityCode}. Reconciles first, like the index.
/// </summary>
public class FacilityModel(BorrowScreenService screens) : PageModel
{
    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Keyword { get; set; }

    [BindProperty(SupportsGet = true, Name = "status")]
    public string? StatusQuery { get; set; }

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

    public int ShownItemCount => Bills.Sum(b => b.Items.Count);
    public int CountFor(BorrowStatusFilter f) => Facility.Bills.Count(b => BorrowWorkboard.Matches(f, b.Status, b.HasConflict));

    public async Task<IActionResult> OnGetAsync(string? facilityCode, CancellationToken cancellationToken)
    {
        FacilityCode = facilityCode?.Trim() ?? string.Empty;
        Keyword = SearchKeyword.Normalize(Keyword);
        Filter = string.IsNullOrEmpty(StatusQuery) ? BorrowStatusFilter.All : BorrowStatusFilterExtensions.Parse(StatusQuery);
        if (!BorrowRules.IsValidFacilityCode(FacilityCode)) return NotFound();

        var s = await screens.TrySyncAsync(cancellationToken);
        Sync = s.Result; SyncError = s.Warning;
        var data = await screens.LoadFacilityAsync(FacilityCode, withHints: true, cancellationToken);
        if (data.Error is not null) { HasError = true; ErrorMessage = data.Error; return Page(); }
        AdvisoryWarning = data.AdvisoryWarning;
        if (data.Bills.Count == 0) return NotFound();

        var name = data.Bills.Select(b => b.Source.FacilityName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));
        Facility = new BorrowFacilityView(FacilityCode, name, data.Bills.OrderByDescending(b => b.Source.DateReceive).ThenByDescending(b => b.Source.SourceRecordNumber).ToList());
        FacilityName = name;
        Bills = Apply(Facility.Bills, Keyword, Filter);
        return Page();
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
