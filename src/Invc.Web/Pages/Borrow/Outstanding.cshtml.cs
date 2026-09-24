using Invc.Core.Borrow;
using Invc.Core.Inventory;
using Invc.Web.Borrow;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Invc.Web.Pages.Borrow;

/// <summary>
/// สรุปคงค้าง — "what still has to be returned", one row per facility × medicine instead of per bill, because the same
/// drug is usually borrowed on several bills. Read-only review screen: returns are recorded on the facility page, not
/// here (internally as well as externally). The data comes from the same two set-based MySQL queries as the work
/// board (<see cref="BorrowScreenService.LoadBoardAsync"/>); the aggregation itself happens in Core, in memory.
/// </summary>
public class OutstandingModel(BorrowScreenService screens) : PageModel
{
    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Keyword { get; set; }

    public BorrowOutstandingSummary Summary { get; private set; } = new([]);
    /// <summary>Totals of the whole board (unfiltered) — tells the user whether an empty result means "nothing outstanding" or "no match".</summary>
    public BorrowWorkboard Board { get; private set; } = new([]);
    public BorrowSyncResult? Sync { get; private set; }
    public string? SyncError { get; private set; }
    public bool HasError { get; private set; }
    public string? ErrorMessage { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Keyword = SearchKeyword.Normalize(Keyword);
        var s = await screens.TrySyncAsync(cancellationToken);
        Sync = s.Result; SyncError = s.Warning;
        var board = await screens.LoadBoardAsync(cancellationToken);
        if (board.Error is not null) { HasError = true; ErrorMessage = board.Error; return; }
        Board = board.Board!;
        Summary = BorrowOutstandingSummary.Build(Board.Facilities, Keyword);
    }
}
