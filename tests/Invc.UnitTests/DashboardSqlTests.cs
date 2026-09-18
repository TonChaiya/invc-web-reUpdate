using Invc.Infrastructure.Dashboard;
using Invc.Infrastructure.Data;

namespace Invc.UnitTests;

public class DashboardSqlTests
{
    [Fact]
    public void Every_dashboard_statement_passes_the_read_only_guard_and_stays_in_its_lane()
    {
        var all = DashboardSql.AllStatements().ToList();
        Assert.Equal(10, all.Count);
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

    [Fact]
    public void Movement_by_item_type_keeps_card_as_source_and_classifies_through_inv_md_ed_ned_and_tbled_ned_safely()
    {
        var sql = DashboardSql.MovementByItemType;
        Assert.Equal(sql, ReadOnlySql.Ensure(sql));
        Assert.Contains("FROM dbo.CARD c", sql, StringComparison.Ordinal);
        Assert.Contains("WHERE c.OPERATE_DATE >= @From AND c.OPERATE_DATE < @To", sql, StringComparison.Ordinal);   // same window as D9
        // classification path: CARD.WORKING_CODE → INV_MD.ED_NED → TBLED_NED (EDCODE/EDNAME); deterministic TOP 1 lookups, no row-multiplying JOIN
        Assert.Contains("OUTER APPLY (SELECT TOP 1 RTRIM(m.ED_NED) AS ItemTypeCode FROM dbo.INV_MD m WHERE m.WORKING_CODE = c.WORKING_CODE ORDER BY m.RECORD_NUMBER) md", sql, StringComparison.Ordinal);
        Assert.Contains("OUTER APPLY (SELECT TOP 1 RTRIM(x.EDNAME) AS ItemTypeName FROM dbo.TBLED_NED x WHERE x.EDCODE = md.ItemTypeCode ORDER BY x.EDCODE) t", sql, StringComparison.Ordinal);
        Assert.DoesNotContain(" JOIN ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LOCATION", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GROUP_CODE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SUPPLY_TYPE", sql, StringComparison.OrdinalIgnoreCase);
        // the same grouping keys as D9 plus the type, so per-month type sums re-add to the D9 totals
        Assert.Contains("GROUP BY YEAR(c.OPERATE_DATE), MONTH(c.OPERATE_DATE), c.R_S_STATUS, LEFT(c.R_S_NUMBER, 1), md.ItemTypeCode, t.ItemTypeName", sql, StringComparison.Ordinal);
    }
}
