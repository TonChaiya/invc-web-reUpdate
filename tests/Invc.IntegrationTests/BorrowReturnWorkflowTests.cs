using Dapper;
using Invc.Core.Borrow;
using Invc.Infrastructure.AppData;
using Invc.Infrastructure.Borrow;
using Microsoft.Extensions.Options;
using MySqlConnector;
using Xunit.Abstractions;

namespace Invc.IntegrationTests;

/// <summary>
/// Return workflow against an ISOLATED MySQL database `invc_web_rw_test` (migrations 001 + 002, dropped at the end).
/// Uses synthetic bills written into the test mirror — SQL Server INV is not touched at all here, and the application
/// database `invc_web` is never opened. Skipped when the MySQL test server is unreachable.
/// </summary>
public class BorrowReturnWorkflowTests(ITestOutputHelper output)
{
    private const string TestDatabase = "invc_web_rw_test";
    private static readonly string TestConnection = (Environment.GetEnvironmentVariable("INVC_TEST_APPDB")
        ?? "Server=127.0.0.1;Port=3306;User ID=root;Password=;CharSet=utf8mb4;Database=invc_web_test").Replace("invc_web_test", TestDatabase);

    [SkippableFact]
    public async Task Returns_corrections_concurrency_rollback_and_history_survive_mirror_reconciliation()
    {
        var factory = await CreateTestDatabaseOrSkipAsync();
        try
        {
            var mirror = new BorrowMirrorRepository(factory);
            var returns = new BorrowReturnRepository(factory);
            // synthetic snapshot: 2 bills (CUB001), 3 items
            BorrowSourceItem I(int rn, int bill, string code, decimal qty) => new() { SourceRecordNumber = rn, SourceBillRecordNumber = bill, ReceiveNo = "T" + bill, WorkingCode = code, DrugName = "Drug " + code, QtyOrder = qty, PackRatio = 1 };
            var b1 = new BorrowSourceBill { SourceRecordNumber = 1, ReceiveNo = "T1", FacilityCode = "CUB001", FacilityName = "ทดสอบ", DateReceive = new DateTime(2026, 9, 1), Items = [I(11, 1, "1000010", 100), I(12, 1, "1000020", 40)] };
            var b2 = new BorrowSourceBill { SourceRecordNumber = 2, ReceiveNo = "T2", FacilityCode = "CUB001", FacilityName = "ทดสอบ", DateReceive = new DateTime(2026, 9, 5), Items = [I(21, 2, "1000030", 10)] };
            var plan = BorrowReconciliationPlan.Build([b1, b2], new BorrowMirrorState(new Dictionary<int, string>(), new Dictionary<int, string>()));
            await mirror.ApplyAsync(plan, DateTime.Now);
            var now = new DateTime(2026, 9, 22, 10, 0, 0);

            // 1. partial return + validation
            var r1 = await returns.RecordReturnAsync(new BorrowReturnRequest(11, 1, 30, now, "tester", "รอบแรก", Guid.NewGuid().ToString()));
            Assert.True(r1.Succeeded, r1.Error);
            Assert.False((await returns.RecordReturnAsync(new BorrowReturnRequest(11, 1, 71, now, "tester", null, null))).Succeeded);   // over
            Assert.False((await returns.RecordReturnAsync(new BorrowReturnRequest(11, 2, 1, now, "tester", null, null))).Succeeded);    // bill mismatch
            Assert.False((await returns.RecordReturnAsync(new BorrowReturnRequest(999, 1, 1, now, "tester", null, null))).Succeeded);   // unknown item
            Assert.False((await returns.RecordReturnAsync(new BorrowReturnRequest(11, 1, 0, now, "tester", null, null))).Succeeded);    // zero
            var bal = (await returns.GetBalancesAsync("CUB001")).Single();
            Assert.Equal(11, bal.SourceItemRecordNumber); Assert.Equal(30m, bal.ReturnedQty); Assert.Equal(now, bal.LastReturnAt);

            // 2. idempotent double submit
            var cid = Guid.NewGuid().ToString();
            var a = await returns.RecordReturnAsync(new BorrowReturnRequest(11, 1, 10, now, "tester", null, cid));
            var b = await returns.RecordReturnAsync(new BorrowReturnRequest(11, 1, 10, now, "tester", null, cid));
            Assert.True(a.Succeeded && b.Succeeded && b.WasDuplicate); Assert.Equal(a.EventIds[0], b.EventIds[0]);
            Assert.Equal(40m, (await returns.GetBalancesAsync("CUB001")).Single().ReturnedQty);

            // 3. concurrency: 20 parallel returns of 5 against 60 outstanding → exactly 12 succeed, never more than borrowed
            var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() => returns.RecordReturnAsync(new BorrowReturnRequest(11, 1, 5, now, "racer", null, null)))));
            output.WriteLine($"concurrent: {results.Count(r => r.Succeeded)} succeeded, {results.Count(r => !r.Succeeded)} rejected");
            Assert.Equal(12, results.Count(r => r.Succeeded));
            Assert.Equal(100m, (await returns.GetBalancesAsync("CUB001")).Single(x => x.SourceItemRecordNumber == 11).ReturnedQty);
            Assert.Equal("รายการนี้คืนครบแล้ว", (await returns.RecordReturnAsync(new BorrowReturnRequest(11, 1, 1, now, "tester", null, null))).Error);

            // 4. bill return: item 12 (40 open) gets one RETURN, item 11 (closed) none; whole thing in one transaction
            var bill = await returns.RecordBillReturnAsync(1, now.AddHours(1), "tester", "ทั้งบิล", Guid.NewGuid().ToString());
            Assert.True(bill.Succeeded, bill.Error); Assert.Single(bill.EventIds);
            Assert.Equal("บิลนี้ไม่มีรายการคงค้าง", (await returns.RecordBillReturnAsync(1, now, "tester", null, null)).Error);
            Assert.Equal("ไม่พบบิลนี้ในข้อมูลที่ซิงก์ไว้", (await returns.RecordBillReturnAsync(777, now, "tester", null, null)).Error);

            // 5. correction: reversible bounded, original untouched, net adjusts; second correction limited by remaining
            var first = r1.EventIds[0];
            var ev = await returns.GetEventAsync(first);
            Assert.Equal(30m, ev!.ReversibleQty);
            Assert.False((await returns.RecordCorrectionAsync(new BorrowCorrectionRequest(first, 31, now, "fixer", "มากไป", null))).Succeeded);
            Assert.False((await returns.RecordCorrectionAsync(new BorrowCorrectionRequest(first, 5, now, "fixer", "  ", null))).Succeeded);     // reason required
            var c1 = await returns.RecordCorrectionAsync(new BorrowCorrectionRequest(first, 20, now.AddHours(2), "fixer", "บันทึกผิด", null));
            Assert.True(c1.Succeeded, c1.Error);
            Assert.Equal(10m, (await returns.GetEventAsync(first))!.ReversibleQty);
            Assert.Equal(30m, (await returns.GetEventAsync(first))!.QuantityDelta);                                    // original row unchanged
            Assert.Equal("ย้อนกลับได้ไม่เกิน 10", (await returns.RecordCorrectionAsync(new BorrowCorrectionRequest(first, 11, now, "fixer", "x", null))).Error);
            Assert.False((await returns.RecordCorrectionAsync(new BorrowCorrectionRequest(c1.EventIds[0], 1, now, "fixer", "x", null))).Succeeded);   // cannot correct a correction
            Assert.Equal(80m, (await returns.GetBalancesAsync("CUB001")).Single(x => x.SourceItemRecordNumber == 11).ReturnedQty);
            var hist = await returns.GetHistoryAsync(new BorrowHistoryFilter("CUB001", "T1", null, null, null, null));
            Assert.Equal(hist.OrderByDescending(e => e.EventAt).ThenByDescending(e => e.Id).Select(e => e.Id), hist.Select(e => e.Id));   // newest first
            Assert.Contains(hist, e => e.EventType == BorrowReturnEventType.Correction && e.CorrectsEventId == first && e.QuantityDelta == -20m);
            Assert.Single(await returns.GetHistoryAsync(new BorrowHistoryFilter(null, null, null, null, null, "fixer")));
            Assert.Single(await returns.GetHistoryAsync(new BorrowHistoryFilter(null, null, "1000020", null, null, null)));

            // 6. nothing in the trail is ever UPDATEd/DELETEd; mirror reconciliation that deletes the source rows keeps history
            await using (var c = await factory.OpenAsync())
            {
                var before = await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM borrow_return_event");
                var plan2 = BorrowReconciliationPlan.Build([b2], await mirror.GetStateAsync());      // INV "lost" bill T1
                Assert.Equal(1, plan2.BillsToDelete.Count); Assert.Equal(2, plan2.ItemsToDelete.Count);
                await mirror.ApplyAsync(plan2, DateTime.Now);
                Assert.Equal(before, await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM borrow_return_event"));
                Assert.Equal(0, await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM borrow_source_item WHERE source_bill_record_number = 1"));
            }
            var orphaned = await returns.GetHistoryAsync(new BorrowHistoryFilter(null, "T1", null, null, null, null));
            Assert.NotEmpty(orphaned); Assert.All(orphaned.Where(e => e.SourceItemRecordNumber == 11), e => Assert.Equal("Drug 1000010", e.DrugName));   // snapshot columns remain readable
            // a correction on an orphaned event still works (no FK), using the event's own snapshot
            var orphanFix = await returns.RecordCorrectionAsync(new BorrowCorrectionRequest(first, 10, now.AddHours(3), "fixer", "ต้นทางหาย", null));
            Assert.True(orphanFix.Succeeded, orphanFix.Error);

            // 7. source re-appears with a lower quantity than already returned → conflict is visible in the read model, nothing auto-corrected
            var lowered = b1 with { Items = [I(11, 1, "1000010", 50), I(12, 1, "1000020", 40)] };
            await mirror.ApplyAsync(BorrowReconciliationPlan.Build([lowered, b2], await mirror.GetStateAsync()), DateTime.Now);
            var views = BorrowWorkboard.BuildBills(await mirror.GetBillsAsync("CUB001"), await returns.GetBalancesAsync("CUB001"));
            var item11 = views.Single(v => v.Source.SourceRecordNumber == 1).Items.Single(i => i.Source.SourceRecordNumber == 11);
            Assert.Equal(70m, item11.ReturnedQty); Assert.Equal(50m, item11.BorrowedQty); Assert.True(item11.HasConflict); Assert.False(item11.CanReturn);
            Assert.Equal("รายการนี้มีข้อมูลขัดแย้งกับต้นทาง (คืนแล้วมากกว่ายอดยืม) ต้องตรวจสอบก่อน", (await returns.RecordReturnAsync(new BorrowReturnRequest(11, 1, 1, now, "tester", null, null))).Error);

            // 8. rollback: a failing statement inside the bill transaction leaves no partial events (item 21 open; force a CHECK violation via a bad actor length? use a poisoned note > column width)
            var tooLong = new string('x', 600);
            await Assert.ThrowsAnyAsync<MySqlException>(() => returns.RecordBillReturnAsync(2, now, "tester", tooLong, null));
            Assert.Empty(await returns.GetHistoryAsync(new BorrowHistoryFilter(null, "T2", null, null, null, null)));
        }
        finally { await DropTestDatabaseAsync(); }
    }

    private static async Task<IAppDbConnectionFactory> CreateTestDatabaseOrSkipAsync()
    {
        var serverOnly = new MySqlConnectionStringBuilder(TestConnection) { Database = "" }.ConnectionString;
        try
        {
            await using var admin = new MySqlConnection(serverOnly);
            await admin.OpenAsync();
            await admin.ExecuteAsync($"CREATE DATABASE IF NOT EXISTS `{TestDatabase}` CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci");
        }
        catch (Exception ex) { Skip.If(true, $"MySQL test server unavailable: {ex.GetType().Name}: {ex.Message}"); }
        await using (var conn = new MySqlConnection(TestConnection))
        {
            await conn.OpenAsync();
            foreach (var file in new[] { "001_borrow_foundation.sql", "002_borrow_return_workflow.sql" })
            {
                await conn.ExecuteAsync(File.ReadAllText(Path.Combine(FindRepoRoot(), "db", "mysql", file)));
            }
        }
        return new MySqlAppDbConnectionFactory(Options.Create(new AppDatabaseOptions { ConnectionString = TestConnection }));
    }

    private static async Task DropTestDatabaseAsync()
    {
        var serverOnly = new MySqlConnectionStringBuilder(TestConnection) { Database = "" }.ConnectionString;
        await using var admin = new MySqlConnection(serverOnly);
        await admin.OpenAsync();
        await admin.ExecuteAsync($"DROP DATABASE IF EXISTS `{TestDatabase}`");   // only the isolated test database
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Invc.slnx"))) { dir = dir.Parent; }
        return dir!.FullName;
    }
}
