using System.Text.RegularExpressions;
using Invc.Core.Inventory;
using Invc.Infrastructure.Data;
using Invc.Infrastructure.Inventory;

namespace Invc.UnitTests;

/// <summary>Phase 2 unit coverage: search normalisation, working-code validation, lot rules, reconciliation.</summary>
public class SearchKeywordTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("  para  ", "para")]
    [InlineData("\tยาเม็ด\r\n", "ยาเม็ด")]        // Thai text preserved, whitespace trimmed
    [InlineData("1001250", "1001250")]
    public void Normalize_trims_and_collapses_blank(string? raw, string? expected)
        => Assert.Equal(expected, SearchKeyword.Normalize(raw));

    [Fact]
    public void Normalize_bounds_length_to_max()
    {
        var raw = new string('ก', SearchKeyword.MaxLength + 25);
        var result = SearchKeyword.Normalize(raw);
        Assert.NotNull(result);
        Assert.Equal(SearchKeyword.MaxLength, result!.Length);
    }

    [Fact]
    public void Normalize_trims_before_bounding()
    {
        var raw = "   " + new string('x', SearchKeyword.MaxLength) + "   ";
        Assert.Equal(new string('x', SearchKeyword.MaxLength), SearchKeyword.Normalize(raw));
    }
}

public class LikePatternTests
{
    [Theory]
    [InlineData("para", "%para%")]
    [InlineData("50%", "%50[%]%")]
    [InlineData("a_b", "%a[_]b%")]
    [InlineData("[x]", "%[[]x]%")]
    [InlineData("ยา 500 mg", "%ยา 500 mg%")]
    [InlineData("100%_[", "%100[%][_][[]%")]
    public void LikePattern_wraps_and_escapes_wildcards(string keyword, string expected)
    {
        var p = InventoryRepository.LikePattern(keyword);
        Assert.Equal(expected, p.Value);
        Assert.False(p.IsAnsi, "pattern must be nvarchar so Thai matches");
        Assert.True(p.Length >= p.Value!.Length);
    }
}

public class WorkingCodeValidationTests
{
    [Theory]
    [InlineData("1001250", true)]
    [InlineData("1", true)]
    [InlineData("AB12345", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("12345678", false)]        // longer than nvarchar(7)
    [InlineData("1001250'", false)]
    [InlineData("10 250", false)]
    [InlineData("../etc", false)]
    public void IsValidWorkingCode(string? code, bool expected)
        => Assert.Equal(expected, InventoryRepository.IsValidWorkingCode(code));
}

public class LotRulesTests
{
    [Theory]
    [InlineData(300, 100, 3)]
    [InlineData(150, 100, 1.5)]
    [InlineData(0, 100, 0)]
    public void PacksOnHand_divides_by_pack_ratio(decimal qty, decimal pack, decimal expected)
        => Assert.Equal(expected, InventoryRules.PacksOnHand(qty, pack));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void PacksOnHand_is_null_when_pack_ratio_not_positive(decimal pack)
        => Assert.Null(InventoryRules.PacksOnHand(300, pack));

    [Fact]
    public void Reconciliation_matches_when_sums_equal()
    {
        var lots = new List<InventoryLot>
        {
            new() { PackRatio = 100, QtyOnHand = 200, LotValue = 50.00m },
            new() { PackRatio = 100, QtyOnHand = 100, LotValue = 25.50m },
        };
        var r = LotReconciliation.From(300, 75.50m, lots);

        Assert.Equal(2, r.LotCount);
        Assert.True(r.IsConsistent);
        Assert.Equal(0, r.QtyDelta);
        Assert.Equal(0, r.ValueDelta);
    }

    [Fact]
    public void Reconciliation_reports_deltas_and_treats_null_lot_value_as_zero()
    {
        var lots = new List<InventoryLot>
        {
            new() { PackRatio = 10, QtyOnHand = 40, LotValue = null },
        };
        var r = LotReconciliation.From(50, 12.00m, lots);

        Assert.False(r.QtyMatches);
        Assert.False(r.ValueMatches);
        Assert.Equal(10, r.QtyDelta);
        Assert.Equal(12.00m, r.ValueDelta);
    }

    [Fact]
    public void Reconciliation_with_no_lots_is_consistent_only_when_header_is_zero()
    {
        Assert.True(LotReconciliation.From(0, 0, []).IsConsistent);
        Assert.False(LotReconciliation.From(5, 0, []).IsConsistent);
    }

    [Fact]
    public void Value_tolerance_is_half_a_satang()
    {
        var lots = new List<InventoryLot> { new() { PackRatio = 1, QtyOnHand = 1, LotValue = 10.004m } };
        Assert.True(LotReconciliation.From(1, 10.00m, lots).ValueMatches);
        Assert.False(LotReconciliation.From(1, 10.01m, lots).ValueMatches);
    }
}

public class InventorySummaryTests
{
    [Fact]
    public void FromGroups_sums_every_group_including_unknown_codes()
    {
        var groups = new List<EdNedGroup>
        {
            new("1", "ยาในบัญชียาหลักแห่งชาติ", "ED", 173, 1000, 100.10m),
            new("3", "วัสดุการแพทย์", "MES", 86, 500, 50.05m),
            new("9", null, null, 2, 7, 1.00m),
            new(null, null, null, 1, 3, 0.50m),
        };
        var s = InventorySummary.FromGroups(groups);

        Assert.Equal(262, s.ActiveItemCount);
        Assert.Equal(1510, s.TotalQtyOnHand);
        Assert.Equal(151.65m, s.TotalValue);
        Assert.Equal("รหัส 9 (ไม่พบในตาราง)", groups[2].DisplayName);
        Assert.Equal("ไม่ระบุ", groups[3].DisplayName);
    }
}

public class MonthsOfStockEdgeTests
{
    [Theory]
    [InlineData("0.125", "33", "0")]     // 0.125/33 = 0.0038 → rounds to 0.00 → "0"
    [InlineData("1", "3", "0.33")]
    [InlineData("2", "3", "0.67")]
    [InlineData("0.5", "1", "0.5")]      // below one month keeps leading zero, no trailing zero
    [InlineData("-10", "5", "-2")]       // negative stock passes through unchanged (legacy did the same)
    [InlineData("1234567", "1", "1234567")]
    public void MonthsOfStock_decimal_edges(string qty, string rate, string expected)
        => Assert.Equal(expected, InventoryRules.MonthsOfStock(decimal.Parse(qty), decimal.Parse(rate)));

    [Fact]
    public void MonthsOfStock_uses_bankers_rounding_like_vbscript_round()
    {
        // VBScript Round() is banker's rounding: 0.125 → 0.12, 0.135 → 0.14
        Assert.Equal("0.12", InventoryRules.MonthsOfStock(0.125m, 1m));
        Assert.Equal("0.14", InventoryRules.MonthsOfStock(0.135m, 1m));
    }
}

public class Phase2SqlGuardTests
{
    [Fact]
    public void Every_inventory_statement_including_static_readonly_passes_the_guard()
    {
        var type = typeof(InventoryRepository).Assembly.GetType("Invc.Infrastructure.Inventory.InventorySql")!;
        var fields = type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.FieldType == typeof(string)).ToList();

        Assert.True(fields.Count >= 6, "expected StatusAll, StatusSearch, SummaryByEdNed, DetailHeader, DetailLots, LotTotals");
        foreach (var f in fields)
        {
            var sql = (string)f.GetValue(null)!;
            Assert.Equal(sql, ReadOnlySql.Ensure(sql));
            Assert.DoesNotContain("SELECT *", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(".*", sql, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Location_variants_append_one_predicate_before_order_by_and_leave_the_legacy_statement_intact()
    {
        var type = typeof(InventoryRepository).Assembly.GetType("Invc.Infrastructure.Inventory.InventorySql")!;
        string F(string n) => (string)type.GetField(n)!.GetValue(null)!;
        foreach (var (plain, byLoc) in new[] { ("StatusAll", "StatusAllByLocation"), ("StatusSearch", "StatusSearchByLocation"), ("StatusLotsAll", "StatusLotsAllByLocation"), ("StatusLotsSearch", "StatusLotsSearchByLocation") })
        {
            var a = F(plain); var b = F(byLoc);
            Assert.Equal(a, ReadOnlySql.Ensure(a));
            Assert.Equal(b, ReadOnlySql.Ensure(b));
            Assert.Equal(a.Replace("  AND m.LOCATION = @Location\n", ""), b.Replace("  AND m.LOCATION = @Location\n", ""));
            Assert.Single(Regex.Matches(b, "AND m.LOCATION = @Location"));
            Assert.True(b.IndexOf("AND m.LOCATION", StringComparison.Ordinal) < b.LastIndexOf("ORDER BY", StringComparison.Ordinal));
        }
        Assert.DoesNotContain("= @Location", F("StatusAll"));
    }

    [Fact]
    public void Borrowable_join_template_passes_the_guard_with_table_value_constructors()
    {
        var method = typeof(InventoryRepository).Assembly.GetType("Invc.Infrastructure.Inventory.InventorySql")!
            .GetMethod("BorrowableJoin", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var join = (string)method.Invoke(null, ["(VALUES (1, N'A', 5, 'O')) AS v(RECORD_NUMBER, WORKING_CODE, BORROW_QTY, CLOSE_STATUS)",
                                                 "(VALUES (1, N'A', 2)) AS u(VCAR_CODE, WORKING_CODE, BORROW_QTY)"])!;
        var sql = "SELECT m.WORKING_CODE, bo.BORROWABLE FROM (VALUES (N'A')) AS m(WORKING_CODE) " + join;
        Assert.Equal(sql, ReadOnlySql.Ensure(sql));
    }

    [Theory]
    [InlineData("SELECT 1 FROM t WHERE x = 'INSERT'")]        // keyword inside a string literal is still rejected — by design (conservative)
    [InlineData("SELECT CAST(1 AS bit), RIGHT(REPLICATE('0', 20) + 'x', 20) FROM dbo.INV_MD m OUTER APPLY (SELECT TOP 1 1 AS n) a")]
    public void Guard_behaviour_on_phase2_constructs(string sql)
    {
        if (sql.Contains("'INSERT'"))
        {
            Assert.Throws<ReadOnlyViolationException>(() => ReadOnlySql.Ensure(sql));
        }
        else
        {
            Assert.Equal(sql, ReadOnlySql.Ensure(sql));
        }
    }
}
