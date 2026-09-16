using Invc.Core.Receipts;

namespace Invc.UnitTests;

public class ReceiptRulesTests
{
    [Theory]
    [InlineData("O6900085", true)]
    [InlineData("O6800001", true)]
    [InlineData("o6900085", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("O69000851234", false)]   // > 10
    [InlineData("O69'--", false)]
    [InlineData("O69 001", false)]
    [InlineData("../x", false)]
    public void IsValidReceiptNo(string? value, bool expected)
        => Assert.Equal(expected, ReceiptRules.IsValidReceiptNo(value));

    [Theory]
    [InlineData(2026, 9, 30, 2569)]   // last day of FY 2569
    [InlineData(2026, 10, 1, 2570)]   // first day of FY 2570
    [InlineData(2025, 10, 1, 2569)]
    [InlineData(2025, 9, 30, 2568)]
    [InlineData(2025, 8, 20, 2568)]   // earliest production receipt
    public void FiscalYearOf_uses_shared_thai_rule_on_receive_date(int y, int m, int d, int expected)
        => Assert.Equal(expected, ReceiptRules.FiscalYearOf(new DateTime(y, m, d)));

    [Fact]
    public void FiscalYearOf_null_date_is_null()
        => Assert.Null(ReceiptRules.FiscalYearOf(null));

    [Theory]
    [InlineData("2569", 2569)]
    [InlineData(" 2568 ", 2568)]
    [InlineData("69", null)]
    [InlineData("2026", null)]
    [InlineData("x", null)]
    [InlineData(null, null)]
    public void TryParseFiscalYear(string? raw, int? expected)
        => Assert.Equal(expected, ReceiptRules.TryParseFiscalYear(raw));

    [Theory]
    [InlineData("10", "10")]
    [InlineData(" 11 ", "11")]
    [InlineData("all", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("1", null)]         // not two characters
    [InlineData("1'", null)]        // not alphanumeric
    [InlineData("ABC", null)]
    public void NormalizeTypeCode_accepts_only_two_alphanumerics(string? raw, string? expected)
        => Assert.Equal(expected, ReceiptRules.NormalizeTypeCode(raw));

    [Theory]
    [InlineData(24, 12, "2")]
    [InlineData(250, 250, "1")]
    [InlineData(10, 1, "10")]
    [InlineData(5, 2, "2.5")]
    [InlineData(10, 0, null)]
    [InlineData(10, null, null)]
    [InlineData(null, 10, null)]
    public void Packs_guards_zero_and_null_ratio(int? qty, int? ratio, string? expected)
        => Assert.Equal(expected is null ? null : decimal.Parse(expected), ReceiptRules.Packs(qty, ratio));

    [Theory]
    [InlineData(245, 24, 12, "490.00")]    // UNIT_VALUE is per pack: 245 × 24 / 12
    [InlineData(18, 10, 1, "180.00")]
    [InlineData(0, 250, 250, "0.00")]
    [InlineData(null, 10, 1, null)]
    [InlineData(10, 10, 0, null)]
    public void LineValue_is_unit_value_times_packs(int? unitValue, int? qty, int? ratio, string? expected)
        => Assert.Equal(expected is null ? null : decimal.Parse(expected), ReceiptRules.LineValue(unitValue, qty, ratio));
}

public class ReceiptReportTests
{
    private static NonPoReceiptSummary H(string no, string type, string? typeName, DateTime date, int lines, decimal value)
        => new() { ReceiveNo = no, TypeCode = type, TypeName = typeName, DateReceive = date, LineCount = lines, TotalValue = value };

    private static readonly IReadOnlyList<NonPoReceiptSummary> Headers =
    [
        H("O6900001", "11", "ยาเบิกจาก CUP", new DateTime(2025, 10, 1), 5, 100m),
        H("O6900002", "10", "เวชภัณฑ์เบิกจาก CUP", new DateTime(2026, 1, 15), 2, 50m),
        H("O6900003", "09", "ยืม", new DateTime(2026, 9, 15), 1, 0m),
        H("O6900004", "ZZ", null, new DateTime(2026, 9, 16), 3, 7m),     // unknown type
    ];

    [Fact]
    public void Report_counts_types_and_totals_from_the_same_rows()
    {
        var r = ReceiptReport.Build(2569, Headers, typeCode: null, keyword: null);
        Assert.Equal(4, r.HeaderCount);
        Assert.Equal(11, r.LineCount);
        Assert.Equal(157m, r.TotalValue);
        Assert.Equal(4, r.TypeBreakdown.Count);
        Assert.Equal(4, r.Rows.Count);

        var zz = r.TypeBreakdown.Single(t => t.Code == "ZZ");
        Assert.Null(zz.Name);
        Assert.Equal("ไม่พบชื่อประเภท", zz.DisplayName);
        Assert.Equal(1, zz.HeaderCount);
        Assert.Equal(3, zz.LineCount);
        Assert.Equal(r.HeaderCount, r.TypeBreakdown.Sum(t => t.HeaderCount));
        Assert.Equal(r.LineCount, r.TypeBreakdown.Sum(t => t.LineCount));
    }

    [Fact]
    public void Type_filter_restricts_rows_but_breakdown_still_covers_all_loaded_headers()
    {
        var r = ReceiptReport.Build(2569, Headers, typeCode: "10", keyword: null);
        Assert.Single(r.Rows);
        Assert.Equal("O6900002", r.Rows[0].ReceiveNo);
        Assert.Equal(4, r.TypeBreakdown.Count);
        Assert.Equal("10", r.TypeCode);
    }

    [Fact]
    public void Summary_display_helpers()
    {
        var h = Headers[3];
        Assert.Equal("ไม่พบชื่อประเภท", h.TypeDisplayName);
        Assert.Equal("ยาเบิกจาก CUP", Headers[0].TypeDisplayName);
        Assert.Equal(2569, Headers[0].FiscalYear);
        Assert.Equal(2570, H("x", "01", null, new DateTime(2026, 10, 1), 0, 0).FiscalYear);
    }

    [Fact]
    public void Detail_reconciliation_and_line_derivations()
    {
        var lines = new List<NonPoReceiptLine>
        {
            new() { WorkingCode = "3000440", DrugName = "Bandage", QtyOrder = 24, PackRatio = 12, UnitValue = 245m },
            new() { WorkingCode = "3000210", DrugName = null, QtyOrder = 10, PackRatio = 0, UnitValue = 18m },
        };
        var d = new NonPoReceiptDetail
        {
            Header = H("O6900085", "10", "x", new DateTime(2026, 9, 15), 2, 490m) with { TotalItem = 2 },
            Lines = lines,
        };
        Assert.Equal(2m, lines[0].Packs);
        Assert.Equal(490m, lines[0].LineValue);
        Assert.Null(lines[1].Packs);
        Assert.Null(lines[1].LineValue);
        Assert.Equal("Bandage", lines[0].DisplayName);
        Assert.Equal("3000210 (ไม่พบชื่อรายการ)", lines[1].DisplayName);

        var rec = d.Reconciliation;
        Assert.True(rec.ItemCountMatches);
        Assert.Equal(490m, rec.LineValueSum);       // unvaluable line contributes nothing
        Assert.True(rec.ValueMatches);
        Assert.False(rec.AllLinesValuable);

        var empty = new NonPoReceiptDetail { Header = H("O1", "01", null, DateTime.Today, 0, 0m) with { TotalItem = 0 }, Lines = [] };
        Assert.True(empty.Reconciliation.ItemCountMatches);
        Assert.True(empty.Reconciliation.ValueMatches);
    }
}

public class ReceiptDefaultPeriodTests
{
    [Fact]
    public void Prefers_current_fiscal_year_when_it_has_data()
    {
        var chosen = ReceiptDefaultPeriod.Choose([2568, 2569], new DateTime(2026, 9, 16), out var note);
        Assert.Equal(2569, chosen);
        Assert.Null(note);
    }

    [Fact]
    public void Falls_back_to_latest_year_with_data_and_says_so()
    {
        var chosen = ReceiptDefaultPeriod.Choose([2567, 2568], new DateTime(2026, 9, 16), out var note);
        Assert.Equal(2568, chosen);
        Assert.NotNull(note);
    }

    [Fact]
    public void No_data_uses_current_fiscal_year_with_note()
    {
        var chosen = ReceiptDefaultPeriod.Choose([], new DateTime(2026, 10, 1), out var note);
        Assert.Equal(2570, chosen);
        Assert.NotNull(note);
    }
}
