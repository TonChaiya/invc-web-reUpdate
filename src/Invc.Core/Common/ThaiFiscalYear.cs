namespace Invc.Core.Common;

/// <summary>
/// Thai fiscal-year helpers ported from include/inc_functions.asp (YearBudget, CMonth)
/// and the inline CASE expressions in the legacy table_*.asp pages.
/// A fiscal year runs 1 Oct – 30 Sep and is numbered by the Buddhist-era year in which it ends.
/// </summary>
public static class ThaiFiscalYear
{
    public const int BuddhistEraOffset = 543;

    /// <summary>inc_functions.asp YearBudget(): YEAR(d)+543, plus one when MONTH(d) ≥ 10.</summary>
    public static int FromDate(DateOnly date)
        => date.Year + BuddhistEraOffset + (date.Month >= 10 ? 1 : 0);

    public static int FromDate(DateTime date) => FromDate(DateOnly.FromDateTime(date));

    /// <summary>
    /// Legacy PO pages filter with <c>LEFT(PO_NO, 2) = RIGHT(FiscalYear, 2)</c>.
    /// Returns the two-digit prefix for a Buddhist-era fiscal year (2569 → "69").
    /// </summary>
    public static string ToPoNumberPrefix(int buddhistFiscalYear)
    {
        if (buddhistFiscalYear is < 2400 or > 2999)
        {
            throw new ArgumentOutOfRangeException(nameof(buddhistFiscalYear), buddhistFiscalYear,
                "Expected a Buddhist-era year such as 2569.");
        }

        return (buddhistFiscalYear % 100).ToString("00");
    }

    /// <summary>inc_functions.asp CMonth(): Buddhist year followed by two-digit month, e.g. 256909.</summary>
    public static string ToBuddhistYearMonth(DateOnly date)
        => $"{date.Year + BuddhistEraOffset}{date.Month:00}";
}
