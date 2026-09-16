using Invc.Infrastructure.Dashboard;
using Invc.Infrastructure.Data;

namespace Invc.UnitTests;

public class DashboardSqlTests
{
    [Fact]
    public void Every_dashboard_statement_passes_the_read_only_guard_and_stays_in_its_lane()
    {
        var all = DashboardSql.AllStatements().ToList();
        Assert.Equal(9, all.Count);
        foreach (var sql in all)
        {
            Assert.Equal(sql, ReadOnlySql.Ensure(sql));
            Assert.DoesNotContain("SELECT *", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("2010930", sql, StringComparison.Ordinal);                 // legacy hard-coded item must not survive
            Assert.DoesNotContain("OTH_IVO", sql, StringComparison.OrdinalIgnoreCase);       // receipts come from the Receipts module
            Assert.DoesNotContain("MIN_LEVEL", sql, StringComparison.OrdinalIgnoreCase);     // reorder rules come from the Reorder module
        }
        Assert.DoesNotContain("STATUS", DashboardSql.ProcessTime, StringComparison.Ordinal);  // no PO bucket sets in SQL
        Assert.Contains("WHERE c.OPERATE_DATE >= @From AND c.OPERATE_DATE < @To", DashboardSql.Movement, StringComparison.Ordinal);
        Assert.Contains("m.NOUSE IS NULL AND m.OUT_OF_LIST IS NULL AND m.PO_INDIVIDUAL IS NULL", DashboardSql.LegacyEdNed, StringComparison.Ordinal);
        Assert.Contains("a.BuyQty < a.AgreeQty AND a.Expdate > @Today", DashboardSql.ActiveAgreements, StringComparison.Ordinal);
    }
}
