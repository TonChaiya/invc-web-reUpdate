using Invc.Infrastructure.Data;

namespace Invc.UnitTests;

public class ReadOnlySqlTests
{
    [Theory]
    [InlineData("SELECT 1")]
    [InlineData("  select WORKING_CODE from dbo.INV_MD where NOUSE is null")]
    [InlineData("WITH x AS (SELECT 1 AS n) SELECT n FROM x")]
    [InlineData("-- comment\nSELECT LAST_UPDATE, DATE_ENTER FROM dbo.BUYPLAN")]   // keyword-like column names are fine
    public void Ensure_accepts_select_statements(string sql)
        => Assert.Equal(sql, ReadOnlySql.Ensure(sql));

    [Theory]
    [InlineData("UPDATE dbo.INV_MD SET NOUSE = 'Y'")]
    [InlineData("DELETE FROM dbo.MS_PO")]
    [InlineData("INSERT INTO dbo.CARD VALUES (1)")]
    [InlineData("SELECT 1; DROP TABLE dbo.INV_MD")]
    [InlineData("SELECT * INTO dbo.tmp FROM dbo.INV_MD")]
    [InlineData("EXEC sp_who")]
    [InlineData("SELECT 1 /* */ ; TRUNCATE TABLE dbo.CARD")]
    [InlineData("select 1 union select 2 exec xp_cmdshell 'dir'")]
    [InlineData("MERGE dbo.INV_MD AS t USING dbo.INV_MD AS s ON 1=1 WHEN MATCHED THEN DELETE")]
    [InlineData("")]
    public void Ensure_rejects_non_select_or_writes(string sql)
        => Assert.ThrowsAny<Exception>(() => ReadOnlySql.Ensure(sql));

    [Fact]
    public void Every_embedded_inventory_query_passes_the_guard()
    {
        var type = typeof(Invc.Infrastructure.Inventory.InventoryRepository).Assembly
            .GetType("Invc.Infrastructure.Inventory.InventorySql")!;
        var fields = type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string)).ToList();

        Assert.NotEmpty(fields);
        foreach (var f in fields)
        {
            var sql = (string)f.GetRawConstantValue()!;
            Assert.Equal(sql, ReadOnlySql.Ensure(sql));
        }
    }
}
