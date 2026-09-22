using Invc.Core.Borrow;
using Invc.Core.Inventory;
using Invc.Web.Borrow;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Invc.Web.Pages.Borrow;

/// <summary>
/// ยายืมจากหน่วยงานอื่น — operations board: every facility with borrowed / returned / outstanding quantities and a
/// status filter (default = outstanding work). Every visit first reconciles the mirror with the complete INV type-09
/// snapshot; if INV cannot be read the mirror is left untouched and the page still renders the last mirrored state.
/// </summary>
public class IndexModel(BorrowScreenService screens) : PageModel
{
    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Keyword { get; set; }

    [BindProperty(SupportsGet = true, Name = "status")]
    public string? StatusQuery { get; set; }

    public BorrowStatusFilter Filter { get; private set; } = BorrowStatusFilter.Outstanding;
    public BorrowWorkboard Board { get; private set; } = new([]);
    public IReadOnlyList<BorrowFacilityView> Facilities { get; private set; } = [];
    public BorrowSyncResult? Sync { get; private set; }
    public string? SyncError { get; private set; }
    public bool HasError { get; private set; }
    public string? ErrorMessage { get; private set; }
    [TempData] public string? Notice { get; set; }

    public int CountFor(BorrowStatusFilter f) => Board.Facilities.Count(x => BorrowWorkboard.Matches(f, x.Status, x.HasConflict));

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Keyword = SearchKeyword.Normalize(Keyword);
        Filter = BorrowStatusFilterExtensions.Parse(StatusQuery);
        var s = await screens.TrySyncAsync(cancellationToken);
        Sync = s.Result; SyncError = s.Warning;
        var board = await screens.LoadBoardAsync(cancellationToken);
        if (board.Error is not null) { HasError = true; ErrorMessage = board.Error; return; }
        Board = board.Board!;
        Facilities = Board.Facilities
            .Where(f => BorrowWorkboard.Matches(Filter, f.Status, f.HasConflict))
            .Where(f => Keyword is null || Matches(f, Keyword))
            .ToList();
    }

    /// <summary>Keyword matches facility code/name, receive no, invoice no, working code or drug name (case-insensitive contains).</summary>
    internal static bool Matches(BorrowFacilityView f, string keyword)
    {
        bool Has(string? s) => s is not null && s.Contains(keyword, StringComparison.OrdinalIgnoreCase);
        return Has(f.FacilityCode) || Has(f.FacilityName)
            || f.Bills.Any(b => Has(b.Source.ReceiveNo) || Has(b.Source.InvoiceNo) || b.Items.Any(i => Has(i.Source.WorkingCode) || Has(i.Source.DrugName)));
    }
}
