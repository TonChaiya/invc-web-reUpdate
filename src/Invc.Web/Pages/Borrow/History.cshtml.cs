using Invc.Core.Borrow;
using Invc.Core.Inventory;
using Invc.Web.Borrow;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Invc.Web.Pages.Borrow;

/// <summary>ประวัติการคืน — the append-only trail, newest first, filterable by facility / receive no / item / date / actor.</summary>
public class HistoryModel(IBorrowReturnRepository returns, IBorrowMirrorRepository mirror, ILogger<HistoryModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true, Name = "facility")] public string? Facility { get; set; }
    [BindProperty(SupportsGet = true, Name = "receiveNo")] public string? ReceiveNo { get; set; }
    [BindProperty(SupportsGet = true, Name = "item")] public string? Item { get; set; }
    [BindProperty(SupportsGet = true, Name = "from")] public DateTime? From { get; set; }
    [BindProperty(SupportsGet = true, Name = "to")] public DateTime? To { get; set; }
    [BindProperty(SupportsGet = true, Name = "actor")] public string? Actor { get; set; }

    public IReadOnlyList<BorrowReturnEvent> Events { get; private set; } = [];
    public IReadOnlyList<(string Code, string Name)> Facilities { get; private set; } = [];
    public bool HasError { get; private set; }
    public bool IsFiltered => Facility is not null || ReceiveNo is not null || Item is not null || From is not null || To is not null || Actor is not null;
    public decimal ReturnedTotal => Events.Where(e => e.EventType == BorrowReturnEventType.Return).Sum(e => e.QuantityDelta);
    public decimal CorrectedTotal => -Events.Where(e => e.EventType == BorrowReturnEventType.Correction).Sum(e => e.QuantityDelta);
    [TempData] public string? Notice { get; set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        Facility = SearchKeyword.Normalize(Facility); ReceiveNo = SearchKeyword.Normalize(ReceiveNo);
        Item = SearchKeyword.Normalize(Item); Actor = SearchKeyword.Normalize(Actor);
        if (Facility is not null && !BorrowRules.IsValidFacilityCode(Facility)) Facility = null;
        try
        {
            Events = await returns.GetHistoryAsync(new BorrowHistoryFilter(Facility, ReceiveNo, Item, From, To, Actor), ct);
            Facilities = (await mirror.GetBillsAsync(null, ct))
                .Where(b => !string.IsNullOrWhiteSpace(b.FacilityCode))
                .GroupBy(b => b.FacilityCode!.Trim())
                .Select(g => (g.Key, g.Select(b => b.FacilityName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? g.Key))
                .OrderBy(f => f.Item2, StringComparer.Ordinal).ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Borrow history read failed");
            HasError = true;
        }
    }
}
