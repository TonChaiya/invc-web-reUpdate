using Invc.Infrastructure.Data;
using Invc.Infrastructure.Receipts;

namespace Invc.UnitTests;

public class ReceiptSqlTests
{
    [Fact]
    public void Every_receipt_statement_passes_the_read_only_guard()
    {
        var all = NonPoReceiptSql.AllStatements().ToList();
        Assert.Equal(6, all.Count);
        foreach (var sql in all)
        {
            Assert.Equal(sql, ReadOnlySql.Ensure(sql));
            Assert.DoesNotContain("SELECT *", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("MS_PO", sql, StringComparison.OrdinalIgnoreCase);     // module stays separate from purchase orders
            Assert.DoesNotContain("MS_IVO", sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Proven_relationships_and_safe_lookups_are_encoded()
    {
        var h = NonPoReceiptSql.HeadersByPeriod;
        Assert.Contains("FROM dbo.OTH_IVOC GROUP BY RECEIVE_NO) l ON l.RECEIVE_NO = h.RECEIVE_NO", h, StringComparison.Ordinal);   // header ↔ line key
        Assert.Contains("LEFT JOIN dbo.RCV_TYPE t ON t.RCV_TYPE_CODE = RTRIM(h.RCV_TYPE)", h, StringComparison.Ordinal);            // type lookup never drops a header
        Assert.Contains("OUTER APPLY (SELECT TOP 1 c.COMPANY_NAME FROM dbo.COMPANY c WHERE c.COMPANY_CODE = h.DPT_CODE", h, StringComparison.Ordinal);
        Assert.Contains("WHERE h.DATE_RECEIVE >= @From AND h.DATE_RECEIVE < @To", h, StringComparison.Ordinal);                     // typed date window, DATE_RECEIVE
        Assert.DoesNotContain("INNER JOIN", h, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INVOICE_DATE >=", h, StringComparison.Ordinal);
        Assert.DoesNotContain("PO_NO", h, StringComparison.Ordinal);                                                                // no PO prefix rule here

        Assert.Contains("WHERE c.RECEIVE_NO = @ReceiveNo", NonPoReceiptSql.Lines, StringComparison.Ordinal);
        Assert.Contains("OUTER APPLY (SELECT TOP 1 m.DRUG_NAME FROM dbo.INV_MD m WHERE m.WORKING_CODE = c.WORKING_CODE", NonPoReceiptSql.Lines, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(2569, "2025-10-01", "2026-10-01")]
    [InlineData(2568, "2024-10-01", "2025-10-01")]
    [InlineData(2570, "2026-10-01", "2027-10-01")]
    public void FiscalYearWindow_is_1_Oct_to_1_Oct_exclusive(int fy, string from, string to)
    {
        var (f, t) = NonPoReceiptRepository.FiscalYearWindow(fy);
        var inv = System.Globalization.CultureInfo.InvariantCulture;   // th-TH would parse the year as Buddhist era
        Assert.Equal(DateTime.Parse(from, inv), f);
        Assert.Equal(DateTime.Parse(to, inv), t);
        // 30 Sep 23:59:59 belongs to the year, 1 Oct 00:00 to the next.
        Assert.True(t.AddSeconds(-1) < t && t.AddSeconds(-1) >= f);
    }
}
