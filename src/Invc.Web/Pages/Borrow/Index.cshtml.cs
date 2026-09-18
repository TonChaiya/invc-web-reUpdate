using Invc.Core.Borrow;
using Invc.Core.Inventory;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Invc.Web.Pages.Borrow;

/// <summary>
/// ยายืมจากหน่วยงานอื่น — facility overview of INV type-09 receipts. Every visit first reconciles the MySQL mirror with the
/// complete INV snapshot (source read → plan → one transaction); if INV cannot be read the mirror is left untouched and
/// the page still renders the last mirrored state with a warning.
/// </summary>
public class IndexModel(BorrowSyncService sync, IBorrowMirrorRepository mirror, ILogger<IndexModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Keyword { get; set; }

    public BorrowOverview Overview { get; private set; } = new([]);
    public BorrowSyncResult? Sync { get; private set; }
    public string? SyncError { get; private set; }
    public bool HasError { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Keyword = SearchKeyword.Normalize(Keyword);
        try
        {
            Sync = await sync.SyncAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Borrow reconciliation failed; showing the existing mirror");
            SyncError = "ไม่สามารถซิงก์ข้อมูลจาก INV ได้ในขณะนี้ — แสดงข้อมูลที่ซิงก์ไว้ล่าสุด";
        }

        try
        {
            var bills = await mirror.GetBillsAsync(null, cancellationToken);
            Overview = BorrowOverview.FromBills(Filter(bills, Keyword));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Borrow mirror read failed");
            HasError = true;
        }
    }

    /// <summary>Keyword matches facility code/name, receive no, invoice no, working code or drug name (case-insensitive contains).</summary>
    internal static IEnumerable<BorrowSourceBill> Filter(IEnumerable<BorrowSourceBill> bills, string? keyword)
    {
        if (keyword is null) return bills;
        bool Has(string? s) => s is not null && s.Contains(keyword, StringComparison.OrdinalIgnoreCase);
        return bills.Where(b => Has(b.FacilityCode) || Has(b.FacilityName) || Has(b.ReceiveNo) || Has(b.InvoiceNo)
                                || b.Items.Any(i => Has(i.WorkingCode) || Has(i.DrugName)));
    }
}
