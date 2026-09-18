using System.Globalization;

namespace Invc.Core.Inventory;

/// <summary>
/// Owner rule (2026-09-18): every list of medicines on every page is shown alphabetically by drug name (A–Z),
/// not in the legacy WORKING_CODE order. Presentation-only: SQL statements keep their legacy ORDER BY, pages re-sort.
/// Case-insensitive, culture-aware (invariant) so Latin names interleave regardless of case and Thai names sort by
/// their own alphabet after Latin; a null/empty name falls to the end, ties break on working code for a stable order.
/// </summary>
public static class DrugNameOrder
{
    public static readonly StringComparer Comparer = StringComparer.Create(CultureInfo.InvariantCulture, ignoreCase: true);

    public static IReadOnlyList<T> Sort<T>(IEnumerable<T> items, Func<T, string?> name, Func<T, string?>? tieBreak = null)
        => items
            .OrderBy(i => string.IsNullOrWhiteSpace(name(i)) ? 1 : 0)
            .ThenBy(i => name(i)?.Trim(), Comparer)
            .ThenBy(i => tieBreak?.Invoke(i), StringComparer.Ordinal)
            .ToList();
}
