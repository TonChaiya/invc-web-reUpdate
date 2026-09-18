using Dapper;
using Invc.Core.Borrow;
using Invc.Infrastructure.AppData;
using Invc.Infrastructure.Borrow;
using Invc.Infrastructure.Data;
using Microsoft.Extensions.Options;
using MySqlConnector;
using Xunit.Abstractions;

namespace Invc.IntegrationTests;

/// <summary>
/// (1) Read-only parity of the type-09 snapshot against raw SQL on live INV.
/// (2) Reconciliation against an ISOLATED MySQL test database `invc_web_test` (created from db/mysql/001, dropped at the end).
/// INV is never written; the application database `invc_web` is never touched by these tests.
/// Connection for the MySQL test database: env INVC_TEST_APPDB, else Laragon's default local root (no password); skipped when unreachable.
/// </summary>
public class BorrowFoundationTests(ProductionReadOnlyFixture db, ITestOutputHelper output) : IClassFixture<ProductionReadOnlyFixture>
{
    private const string TestDatabase = "invc_web_test";
    private static readonly string TestConnection = Environment.GetEnvironmentVariable("INVC_TEST_APPDB")
        ?? "Server=127.0.0.1;Port=3306;User ID=root;Password=;CharSet=utf8mb4;Database=invc_web_test";

    [SkippableFact]
    public async Task Type09_snapshot_matches_raw_inv_counts_ids_and_sample_values()
    {
        var connections = db.RequireOrSkip();
        var repo = new BorrowSourceRepository(connections);
        var snapshot = await repo.GetSnapshotAsync();

        await using var c = await connections.OpenAsync();
        var headers = await c.ExecuteScalarAsync<int>(ReadOnlySql.Ensure("SELECT COUNT(*) FROM dbo.OTH_IVO WHERE RTRIM(RCV_TYPE) = '09'"));
        var lines = await c.ExecuteScalarAsync<int>(ReadOnlySql.Ensure("SELECT COUNT(*) FROM dbo.OTH_IVOC c JOIN dbo.OTH_IVO h ON h.RECEIVE_NO = c.RECEIVE_NO WHERE RTRIM(h.RCV_TYPE) = '09'"));
        var headerIds = (await c.QueryAsync<int>(ReadOnlySql.Ensure("SELECT RECORD_NUMBER FROM dbo.OTH_IVO WHERE RTRIM(RCV_TYPE) = '09' ORDER BY RECORD_NUMBER"))).ToList();
        var lineIds = (await c.QueryAsync<int>(ReadOnlySql.Ensure("SELECT c.RECORD_NUMBER FROM dbo.OTH_IVOC c JOIN dbo.OTH_IVO h ON h.RECEIVE_NO = c.RECEIVE_NO WHERE RTRIM(h.RCV_TYPE) = '09' ORDER BY c.RECORD_NUMBER"))).ToList();
        var qtyTotal = await c.ExecuteScalarAsync<decimal>(ReadOnlySql.Ensure("SELECT ISNULL(SUM(c.QTY_ORDER),0) FROM dbo.OTH_IVOC c JOIN dbo.OTH_IVO h ON h.RECEIVE_NO = c.RECEIVE_NO WHERE RTRIM(h.RCV_TYPE) = '09'"));
        var dupReceive = await c.ExecuteScalarAsync<int>(ReadOnlySql.Ensure("SELECT COUNT(*) FROM (SELECT RECEIVE_NO FROM dbo.OTH_IVO GROUP BY RECEIVE_NO HAVING COUNT(*) > 1) d"));

        output.WriteLine($"{DateTime.Now:O} type-09 headers {headers}, lines {lines}, qty {qtyTotal}; snapshot bills {snapshot.Count}, items {snapshot.Sum(b => b.Items.Count)}; duplicate RECEIVE_NO {dupReceive}");
        Assert.Equal(0, dupReceive);                      // precondition for the RECEIVE_NO join and the mirror's UNIQUE(receive_no)
        Assert.Equal(headers, snapshot.Count);
        Assert.Equal(lines, snapshot.Sum(b => b.Items.Count));
        Assert.Equal(headerIds, snapshot.Select(b => b.SourceRecordNumber).OrderBy(x => x));
        Assert.Equal(lineIds, snapshot.SelectMany(b => b.Items).Select(i => i.SourceRecordNumber).OrderBy(x => x));
        Assert.Equal(qtyTotal, snapshot.SelectMany(b => b.Items).Sum(i => i.QtyOrder ?? 0m));
        Assert.All(snapshot, b => Assert.All(b.Items, i => Assert.Equal(b.SourceRecordNumber, i.SourceBillRecordNumber)));
        Assert.All(snapshot, b => Assert.All(b.Items, i => Assert.Equal(b.ReceiveNo, i.ReceiveNo)));

        var sample = snapshot.SingleOrDefault(b => b.ReceiveNo == "O6900084");
        Skip.If(sample is null, "sample bill O6900084 no longer exists");
        var raw = await c.QuerySingleAsync<(int Rn, string Dpt, string Name, DateTime? Received, int LineRn, string Code, string Drug, decimal Qty, decimal Pack, string Lot)>(ReadOnlySql.Ensure("""
            SELECT h.RECORD_NUMBER, RTRIM(h.DPT_CODE), co.COMPANY_NAME, h.DATE_RECEIVE, c.RECORD_NUMBER, RTRIM(c.WORKING_CODE), md.DRUG_NAME, c.QTY_ORDER, c.PACK_RATIO, c.LOTNO
            FROM dbo.OTH_IVO h JOIN dbo.OTH_IVOC c ON c.RECEIVE_NO = h.RECEIVE_NO
            OUTER APPLY (SELECT TOP 1 x.COMPANY_NAME FROM dbo.COMPANY x WHERE x.COMPANY_CODE = h.DPT_CODE ORDER BY x.RECORD_NUMBER) co
            OUTER APPLY (SELECT TOP 1 m.DRUG_NAME FROM dbo.INV_MD m WHERE m.WORKING_CODE = c.WORKING_CODE ORDER BY m.RECORD_NUMBER) md
            WHERE h.RECEIVE_NO = 'O6900084'
            """));
        output.WriteLine($"O6900084: header {raw.Rn} {raw.Dpt} {raw.Name} {raw.Received:d}; line {raw.LineRn} {raw.Code} {raw.Drug} qty {raw.Qty} pack {raw.Pack} lot {raw.Lot}");
        Assert.Equal(raw.Rn, sample!.SourceRecordNumber); Assert.Equal(raw.Dpt, sample.FacilityCode); Assert.Equal(raw.Name, sample.FacilityName); Assert.Equal(raw.Received, sample.DateReceive);
        var line = Assert.Single(sample.Items);
        Assert.Equal(raw.LineRn, line.SourceRecordNumber); Assert.Equal(raw.Code, line.WorkingCode); Assert.Equal(raw.Drug, line.DrugName);
        Assert.Equal(raw.Qty, line.QtyOrder); Assert.Equal(raw.Pack, line.PackRatio); Assert.Equal(raw.Lot, line.LotNo);
    }

    [SkippableFact]
    public async Task Mirror_reconciliation_inserts_updates_deletes_and_rolls_back_in_an_isolated_test_database()
    {
        var connections = db.RequireOrSkip();
        var factory = await CreateTestDatabaseOrSkipAsync();
        try
        {
            var source = new BorrowSourceRepository(connections);
            var mirror = new BorrowMirrorRepository(factory);
            var service = new BorrowSyncService(source, mirror);

            // 1. empty mirror → everything inserted
            var r1 = await service.SyncAsync();
            output.WriteLine($"sync#1 inserted {r1.BillsInserted}/{r1.ItemsInserted} updated {r1.Updated} deleted {r1.Deleted}");
            Assert.Equal(r1.SourceBills, r1.BillsInserted); Assert.Equal(r1.SourceItems, r1.ItemsInserted);
            Assert.Equal(0, r1.Updated + r1.Deleted);
            var bills = await mirror.GetBillsAsync(null);
            Assert.Equal(r1.SourceBills, bills.Count); Assert.Equal(r1.SourceItems, bills.Sum(b => b.Items.Count));
            Assert.True(bills.Zip(bills.Skip(1)).All(p => (p.First.DateReceive ?? DateTime.MinValue) >= (p.Second.DateReceive ?? DateTime.MinValue)), "newest first");

            // 2. second run: nothing changes
            var r2 = await service.SyncAsync();
            Assert.Equal(0, r2.Inserted + r2.Updated + r2.Deleted); Assert.Equal(r1.SourceBills, r2.BillsUnchanged); Assert.Equal(r1.SourceItems, r2.ItemsUnchanged);

            // 3. tamper with the MIRROR (simulates an edit that INV no longer agrees with) + add a stale bill/item that INV does not have
            await using (var m = await factory.OpenAsync())
            {
                var anyItem = bills.SelectMany(b => b.Items).First();
                await m.ExecuteAsync("UPDATE borrow_source_item SET qty_order = qty_order + 1, source_hash = 'tampered' WHERE source_record_number = @Id", new { Id = anyItem.SourceRecordNumber });
                await m.ExecuteAsync("INSERT INTO borrow_source_bill (source_record_number, receive_no, facility_code, facility_name, source_hash, last_synced_at) VALUES (-1, 'ZZTEST', 'ZZZ', 'stale', 'x', NOW())");
                await m.ExecuteAsync("INSERT INTO borrow_source_item (source_record_number, source_bill_record_number, receive_no, working_code, source_hash, last_synced_at) VALUES (-1, -1, 'ZZTEST', '0000000', 'x', NOW())");
            }
            var r3 = await service.SyncAsync();
            output.WriteLine($"sync#3 updated items {r3.ItemsUpdated}, deleted bills {r3.BillsDeleted}, deleted items {r3.ItemsDeleted}");
            Assert.Equal(1, r3.ItemsUpdated); Assert.Equal(1, r3.BillsDeleted); Assert.Equal(1, r3.ItemsDeleted); Assert.Equal(0, r3.Inserted);
            var after = await mirror.GetBillsAsync(null);
            Assert.Equal(r1.SourceBills, after.Count);
            Assert.DoesNotContain(after, b => b.ReceiveNo == "ZZTEST");
            var restored = after.SelectMany(b => b.Items).Single(i => i.SourceRecordNumber == bills.SelectMany(b => b.Items).First().SourceRecordNumber);
            Assert.Equal(bills.SelectMany(b => b.Items).First().QtyOrder, restored.QtyOrder);

            // 4. a failing transaction leaves the mirror untouched (item referencing a non-existent bill violates the FK)
            var badPlan = new BorrowReconciliationPlan(
                [], [], [], 0,
                [new BorrowSourceItem { SourceRecordNumber = -2, SourceBillRecordNumber = -999, ReceiveNo = "BAD", WorkingCode = "0000000" }], [], [], 0);
            await Assert.ThrowsAnyAsync<MySqlException>(() => mirror.ApplyAsync(badPlan, DateTime.Now));
            var state = await mirror.GetStateAsync();
            Assert.DoesNotContain(-2, state.ItemHashes.Keys);
            Assert.Equal(r1.SourceItems, state.ItemHashes.Count);

            // 5. facility filter
            var facility = after.First().FacilityCode!;
            var only = await mirror.GetBillsAsync(facility);
            Assert.All(only, b => Assert.Equal(facility, b.FacilityCode));
            Assert.Equal(after.Count(b => b.FacilityCode == facility), only.Count);
        }
        finally
        {
            await DropTestDatabaseAsync();
        }
    }

    private static async Task<IAppDbConnectionFactory> CreateTestDatabaseOrSkipAsync()
    {
        var builder = new MySqlConnectionStringBuilder(TestConnection);
        Skip.If(!string.Equals(builder.Database, TestDatabase, StringComparison.OrdinalIgnoreCase), $"INVC_TEST_APPDB must name the isolated database {TestDatabase}");
        var serverOnly = new MySqlConnectionStringBuilder(TestConnection) { Database = "" }.ConnectionString;
        try
        {
            await using var admin = new MySqlConnection(serverOnly);
            await admin.OpenAsync();
            await admin.ExecuteAsync($"CREATE DATABASE IF NOT EXISTS `{TestDatabase}` CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci");
        }
        catch (Exception ex)
        {
            Skip.If(true, $"MySQL test server unavailable: {ex.GetType().Name}: {ex.Message}");
        }
        var migration = File.ReadAllText(Path.Combine(FindRepoRoot(), "db", "mysql", "001_borrow_foundation.sql"));
        await using (var conn = new MySqlConnection(TestConnection))
        {
            await conn.OpenAsync();
            await conn.ExecuteAsync(migration);
        }
        return new MySqlAppDbConnectionFactory(Options.Create(new AppDatabaseOptions { ConnectionString = TestConnection }));
    }

    private static async Task DropTestDatabaseAsync()
    {
        var serverOnly = new MySqlConnectionStringBuilder(TestConnection) { Database = "" }.ConnectionString;
        await using var admin = new MySqlConnection(serverOnly);
        await admin.OpenAsync();
        await admin.ExecuteAsync($"DROP DATABASE IF EXISTS `{TestDatabase}`");   // only the isolated test database, never invc_web
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Invc.slnx"))) { dir = dir.Parent; }
        return dir!.FullName;
    }
}
