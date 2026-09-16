using Invc.Core.Common;

namespace Invc.UnitTests;

public class ThaiFiscalYearTests
{
    [Theory]
    [InlineData(2026, 3, 19, 2569)]   // the one production PO: 2026-03-19 → FY 2569
    [InlineData(2026, 9, 30, 2569)]
    [InlineData(2026, 10, 1, 2570)]   // October starts the next fiscal year
    [InlineData(2025, 12, 31, 2569)]
    public void FromDate_matches_inc_functions_YearBudget(int y, int m, int d, int expected)
        => Assert.Equal(expected, ThaiFiscalYear.FromDate(new DateOnly(y, m, d)));

    [Theory]
    [InlineData(2569, "69")]
    [InlineData(2570, "70")]
    [InlineData(2600, "00")]
    public void ToPoNumberPrefix_uses_last_two_digits(int fy, string expected)
        => Assert.Equal(expected, ThaiFiscalYear.ToPoNumberPrefix(fy));

    [Fact]
    public void ToPoNumberPrefix_rejects_gregorian_years()
        => Assert.Throws<ArgumentOutOfRangeException>(() => ThaiFiscalYear.ToPoNumberPrefix(2026));

    [Fact]
    public void ToBuddhistYearMonth_matches_CMonth()
        => Assert.Equal("256909", ThaiFiscalYear.ToBuddhistYearMonth(new DateOnly(2026, 9, 16)));
}
