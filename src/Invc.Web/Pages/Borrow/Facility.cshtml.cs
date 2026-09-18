using Invc.Core.Borrow;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Invc.Web.Pages.Borrow;

/// <summary>Bills of one facility (newest first) with their items. Route: /Borrow/Facility/{facilityCode}. Reconciles first, like the index.</summary>
public class FacilityModel(BorrowSyncService sync, IBorrowMirrorRepository mirror, ILogger<FacilityModel> logger) : PageModel
{
    /// <summary>Optional keyword: limits the shown bills/items to those matching drug name, working code, lot or receive/invoice no.</summary>
    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Keyword { get; set; }

    public string FacilityCode { get; private set; } = string.Empty;
    /// <summary>All bills of the facility (unfiltered) — the header totals are always the full picture.</summary>
    public IReadOnlyList<BorrowSourceBill> AllBills { get; private set; } = [];
    public string? FacilityName { get; private set; }
    public IReadOnlyList<BorrowSourceBill> Bills { get; private set; } = [];
    public BorrowSyncResult? Sync { get; private set; }
    public string? SyncError { get; private set; }
    public bool HasError { get; private set; }

    public int ItemCount => AllBills.Sum(b => b.Items.Count);
    public decimal TotalQty => AllBills.Sum(b => b.Items.Sum(i => i.QtyOrder ?? 0m));
    public int ShownItemCount => Bills.Sum(b => b.Items.Count);

    public async Task<IActionResult> OnGetAsync(string? facilityCode, CancellationToken cancellationToken)
    {
        FacilityCode = facilityCode?.Trim() ?? string.Empty;
        Keyword = Invc.Core.Inventory.SearchKeyword.Normalize(Keyword);
        if (!BorrowRules.IsValidFacilityCode(FacilityCode))
        {
            return NotFound();
        }

        try
        {
            Sync = await sync.SyncAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Borrow reconciliation failed on facility page; showing the existing mirror");
            SyncError = "ไม่สามารถซิงก์ข้อมูลจาก INV ได้ในขณะนี้ — แสดงข้อมูลที่ซิงก์ไว้ล่าสุด";
        }

        try
        {
            AllBills = (await mirror.GetBillsAsync(FacilityCode, cancellationToken))
                .Select(b => b with { Items = Invc.Core.Inventory.DrugNameOrder.Sort(b.Items, i => i.DrugName, i => i.WorkingCode) })
                .ToList();   // owner rule: item lists A–Z by drug name
            Bills = Filter(AllBills, Keyword);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Borrow mirror read failed (facility {FacilityCode})", FacilityCode);
            HasError = true;
            return Page();
        }

        if (AllBills.Count == 0)
        {
            return NotFound();
        }
        FacilityName = AllBills.Select(b => b.FacilityName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));
        return Page();
    }

    /// <summary>Bills whose receive/invoice no. matches keep all items; otherwise only the matching items are kept (bills with none drop out).</summary>
    internal static IReadOnlyList<BorrowSourceBill> Filter(IReadOnlyList<BorrowSourceBill> bills, string? keyword)
    {
        if (keyword is null) return bills;
        bool Has(string? s) => s is not null && s.Contains(keyword, StringComparison.OrdinalIgnoreCase);
        var result = new List<BorrowSourceBill>();
        foreach (var b in bills)
        {
            if (Has(b.ReceiveNo) || Has(b.InvoiceNo)) { result.Add(b); continue; }
            var items = b.Items.Where(i => Has(i.DrugName) || Has(i.WorkingCode) || Has(i.LotNo)).ToList();
            if (items.Count > 0) result.Add(b with { Items = items });
        }
        return result;
    }
}
