using Invc.Core.Inventory;

namespace Invc.Core.Reorder;

/// <summary>
/// The reorder recommendation report: every eligible item classified once, then filtered.
/// Counts and the visible rows are derived from the same in-memory list, exactly as the legacy page
/// classified every row in VBScript and displayed only those matching the selected status.
/// </summary>
public sealed class ReorderReport
{
    private ReorderReport(IReadOnlyList<ReorderItem> eligible, ReorderStatusFilter filter, string? keyword)
    {
        Eligible = eligible;
        Filter = filter;
        Keyword = keyword;
        RedCount = eligible.Count(i => i.Status == ReorderStatus.Red);
        YellowCount = eligible.Count(i => i.Status == ReorderStatus.Yellow);
        GreenCount = eligible.Count(i => i.Status == ReorderStatus.Green);
        Rows = eligible.Where(i => filter.Matches(i.Status)).ToList();
        RedSuggestedTotal = eligible.Where(i => i.Status == ReorderStatus.Red).Sum(i => i.SuggestedOrderQty);
    }

    /// <summary>All eligible items (legacy filter, optionally keyword-limited), in report order.</summary>
    public IReadOnlyList<ReorderItem> Eligible { get; }
    public ReorderStatusFilter Filter { get; }
    public string? Keyword { get; }

    /// <summary>Items matching the selected status filter.</summary>
    public IReadOnlyList<ReorderItem> Rows { get; }

    public int EligibleCount => Eligible.Count;
    public int RedCount { get; }
    public int YellowCount { get; }
    public int GreenCount { get; }

    /// <summary>Parity B3: Σ raw suggested quantity over RED items.</summary>
    public decimal RedSuggestedTotal { get; }

    public int MissingMinCount => Eligible.Count(i => (i.MinLevel ?? 0m) == 0m);
    public int MissingMaxCount => Eligible.Count(i => (i.MaxLevel ?? 0m) == 0m);

    public static ReorderReport Build(IReadOnlyList<ReorderItem> eligible, ReorderStatusFilter filter, string? keyword = null)
        => new(eligible, filter, keyword);
}

/// <summary>Read-only source of eligible reorder rows (dbo.INV_MD).</summary>
public interface IReorderRepository
{
    /// <summary>
    /// Eligible rows with the legacy filter
    /// <c>(NOUSE IS NULL OR NOUSE='') AND (OUT_OF_LIST IS NULL OR OUT_OF_LIST='')</c>,
    /// optionally limited by a normalised keyword. Classification happens in Core.
    /// </summary>
    Task<IReadOnlyList<ReorderItem>> GetEligibleAsync(string? keyword, CancellationToken cancellationToken = default);
}
