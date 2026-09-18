using System.Text.RegularExpressions;
using Invc.Infrastructure.AppData;
using Invc.Infrastructure.Borrow;
using Invc.Infrastructure.Data;

namespace Invc.UnitTests;

public class BorrowInfrastructureTests
{
    [Fact]
    public void Inv_source_statements_are_single_selects_accepted_by_the_read_only_guard()
    {
        Assert.Equal(BorrowSourceSql.Headers, ReadOnlySql.Ensure(BorrowSourceSql.Headers));
        Assert.Equal(BorrowSourceSql.Lines, ReadOnlySql.Ensure(BorrowSourceSql.Lines));
        foreach (var sql in new[] { BorrowSourceSql.Headers, BorrowSourceSql.Lines })
        {
            Assert.DoesNotMatch(@"(?i)\b(INSERT|UPDATE|DELETE|MERGE|TRUNCATE|CREATE|ALTER|DROP|EXEC)\b", sql);
            Assert.Contains("RTRIM(h.RCV_TYPE) = @TypeCode", sql);      // type 09 is a parameter, never concatenated
            Assert.Contains("dbo.OTH_IVO", sql);
        }
        Assert.Contains("OUTER APPLY (SELECT TOP 1 x.COMPANY_NAME", BorrowSourceSql.Headers);
        Assert.Contains("JOIN dbo.OTH_IVO h ON h.RECEIVE_NO = c.RECEIVE_NO", BorrowSourceSql.Lines);
    }

    [Fact]
    public void Mirror_dml_lives_only_in_the_mysql_repository_and_targets_only_borrow_tables()
    {
        var dml = new[] { BorrowMirrorSql.UpsertBill, BorrowMirrorSql.UpsertItem, BorrowMirrorSql.DeleteItems, BorrowMirrorSql.DeleteBills };
        foreach (var sql in dml)
        {
            Assert.DoesNotContain("dbo.", sql);                              // never a SQL Server object
            Assert.Matches(@"(?i)^\s*(INSERT INTO|DELETE FROM) borrow_source_(bill|item)\b", sql);
            Assert.Throws<ReadOnlyViolationException>(() => ReadOnlySql.Ensure(sql));   // the INV guard would refuse it
        }
        Assert.DoesNotMatch(@"(?i)\b(DROP|TRUNCATE|ALTER|CREATE)\b", string.Join("\n", dml));

        // the INV-side repository source contains no DML at all
        var infra = typeof(BorrowSourceRepository).Assembly;
        Assert.NotNull(infra.GetType("Invc.Infrastructure.Borrow.BorrowMirrorSql"));
        Assert.Null(typeof(Invc.Core.Borrow.BorrowRules).Assembly.GetType("Invc.Core.Borrow.BorrowMirrorSql"));   // Core has no SQL
    }

    [Theory]
    [InlineData("Server=DESKTOP-BVH8F8L;Initial Catalog=INV;Integrated Security=True")]
    [InlineData("Data Source=.;Database=invc_web")]
    [InlineData("Server=127.0.0.1;Database=INV;User ID=root")]
    [InlineData("Server=127.0.0.1;Database=inv;User ID=root")]
    [InlineData("Server=127.0.0.1;Database=invc_web;User ID=sa;Password=x")]
    [InlineData("Server=127.0.0.1;User ID=root")]
    public void App_database_factory_refuses_sql_server_shapes_the_inv_database_and_sa(string connectionString)
    {
        Assert.Throws<InvalidOperationException>(() => MySqlAppDbConnectionFactory.Validate(connectionString));
    }

    [Fact]
    public void App_database_factory_accepts_a_mysql_application_database()
    {
        var b = MySqlAppDbConnectionFactory.Validate("Server=127.0.0.1;Port=3306;Database=invc_web;User ID=root;Password=;");
        Assert.Equal("invc_web", b.Database);
    }

    [Fact]
    public void Migration_script_targets_only_borrow_tables_and_never_drops()
    {
        var root = FindRepoRoot();
        var sql = File.ReadAllText(Path.Combine(root, "db", "mysql", "001_borrow_foundation.sql"));
        Assert.DoesNotMatch(@"(?i)\bDROP\b|\bTRUNCATE\b|\bUSE\s+INV\b|\bdbo\.", sql);
        Assert.Contains("CREATE TABLE IF NOT EXISTS borrow_source_bill", sql);
        Assert.Contains("CREATE TABLE IF NOT EXISTS borrow_source_item", sql);
        Assert.Contains("ON DELETE CASCADE", sql);
        Assert.Contains("UNIQUE KEY ux_borrow_source_bill_receive_no (receive_no)", sql);
        Assert.Equal(3, Regex.Matches(sql, @"CREATE TABLE IF NOT EXISTS").Count);   // schema_version + 2 mirror tables, nothing else
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Invc.slnx"))) { dir = dir.Parent; }
        return dir!.FullName;
    }
}
