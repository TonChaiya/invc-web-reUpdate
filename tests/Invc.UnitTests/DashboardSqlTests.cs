using Invc.Infrastructure.Dashboard;
using Invc.Infrastructure.Data;

namespace Invc.UnitTests;

public class DashboardSqlTests
{
    [Fact]
    public void Every_dashboard_statement_passes_the_read_only_guard_and_stays_in_its_lane()
    {
        var all = DashboardSql.AllStatements().ToList();
        Assert.Equal(10, all.Count);   // Budget, Substock, StockCoverage, LegacyEdNed, ActiveAgreements, ProcessedSnapshots, ProcessedFlows, ProcessTime, ItemHeader, ItemTrend
        foreach (var sql in all)
        {
            Assert.Equal(sql, ReadOnlySql.Ensure(sql));
            Assert.DoesNotContain("SELECT *", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("2010930", sql, StringComparison.Ordinal);                 // legacy hard-coded item must not survive
            Assert.DoesNotContain("OTH_IVO", sql, StringComparison.OrdinalIgnoreCase);       // receipts come from the Receipts module
            Assert.DoesNotContain("MIN_LEVEL", sql, StringComparison.OrdinalIgnoreCase);     // reorder rules come from the Reorder module
        }
        Assert.DoesNotContain("STATUS", DashboardSql.ProcessTime, StringComparison.Ordinal);  // no PO bucket sets in SQL
        Assert.Contains("WHERE c.WORKING_CODE = @WorkingCode", DashboardSql.ItemTrend, StringComparison.Ordinal);   // CARD stays the per-item trend source only
        Assert.Contains("m.NOUSE IS NULL AND m.OUT_OF_LIST IS NULL AND m.PO_INDIVIDUAL IS NULL", DashboardSql.LegacyEdNed, StringComparison.Ordinal);
        Assert.Contains("a.BuyQty < a.AgreeQty AND a.Expdate > @Today", DashboardSql.ActiveAgreements, StringComparison.Ordinal);
    }

    [Fact]
    public void Processed_monthly_statements_use_mnth_sum_and_mbs_re_m_with_historical_ed_ned_and_never_card_or_mbs_re_y()
    {
        var snap = DashboardSql.ProcessedSnapshots; var flow = DashboardSql.ProcessedFlows;
        foreach (var sql in new[] { snap, flow })
        {
            Assert.Equal(sql, ReadOnlySql.Ensure(sql));
            Assert.DoesNotContain("MBS_RE_Y", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("INV_MD", sql, StringComparison.OrdinalIgnoreCase);        // historical ED_NED, not today's classification
            Assert.DoesNotContain("CARD", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("REMAIN_", sql, StringComparison.OrdinalIgnoreCase);       // MBS_RE_M.REMAIN_* is not the store total
            Assert.Contains("OUTER APPLY (SELECT TOP 1 RTRIM(x.EDNAME) AS EdNedName FROM dbo.TBLED_NED x", sql, StringComparison.Ordinal);
            Assert.Contains("RIGHT('0' + RTRIM(", sql, StringComparison.Ordinal);            // CE yyyymm text keys, zero-padded month
            Assert.Contains(">= @FromKey", sql); Assert.Contains("<= @ToKey", sql);
            Assert.DoesNotContain(" JOIN ", sql, StringComparison.OrdinalIgnoreCase);
        }
        Assert.Contains("FROM dbo.MNTH_SUM s", snap); Assert.Contains("SUM(s.QTY_REMAIN)", snap); Assert.Contains("SUM(s.TOTAL_VALUE)", snap); Assert.Contains("RTRIM(s.ED_NED) AS EdNed", snap);
        Assert.Contains("FROM dbo.MBS_RE_M m", flow); Assert.Contains("SUM(m.RCV_VALUE)", flow); Assert.Contains("SUM(m.SALE_VALUE)", flow); Assert.Contains("RTRIM(m.ED_NED) AS EdNed", flow);
        // CARD no longer owns the monthly summary: no dashboard statement groups CARD by month except the per-item trend
        foreach (var sql in DashboardSql.AllStatements().Where(x => x.Contains("dbo.CARD", StringComparison.OrdinalIgnoreCase)))
        {
            Assert.Contains("@WorkingCode", sql);
        }
    }
}
