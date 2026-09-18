using Invc.Core.Borrow;

namespace Invc.UnitTests;

public class BorrowCoreTests
{
    private static BorrowSourceItem Item(int rn, int billRn, string code = "1001440", decimal qty = 250, decimal pack = 250, string lot = "808640.")
        => new() { SourceRecordNumber = rn, SourceBillRecordNumber = billRn, ReceiveNo = "O6900084", WorkingCode = code, DrugName = "AMILORIDE", QtyOrder = qty, PackRatio = pack, LotNo = lot, ManufacCode = "CUB001", VendorCode = "CUB001" };

    private static BorrowSourceBill Bill(int rn, string receiveNo = "O6900084", string facility = "CUB001", DateTime? received = null, params BorrowSourceItem[] items)
        => new() { SourceRecordNumber = rn, ReceiveNo = receiveNo, InvoiceNo = "24082569", InvoiceDate = new DateTime(2026, 8, 24), DateReceive = received ?? new DateTime(2026, 8, 24), FacilityCode = facility, FacilityName = "ศูนย์สาธารณสุขและการแพทย์ตำบลทรายมูล", SourceSysdate = new DateTime(2026, 9, 11, 9, 34, 52), Items = items };

    [Fact]
    public void Hash_is_deterministic_and_sensitive_to_every_source_field_but_not_sync_time()
    {
        var a = Bill(113, items: Item(1036, 113));
        var b = Bill(113, items: Item(1036, 113));
        Assert.Equal(a.ComputeHash(), b.ComputeHash());
        Assert.Equal(64, a.ComputeHash().Length);
        Assert.Equal(a.Items[0].ComputeHash(), b.Items[0].ComputeHash());

        Assert.NotEqual(a.ComputeHash(), (a with { FacilityName = "x" }).ComputeHash());
        Assert.NotEqual(a.ComputeHash(), (a with { DateReceive = new DateTime(2026, 8, 25) }).ComputeHash());
        Assert.NotEqual(a.ComputeHash(), (a with { InvoiceNo = null }).ComputeHash());
        Assert.NotEqual(a.Items[0].ComputeHash(), (a.Items[0] with { QtyOrder = 251 }).ComputeHash());
        Assert.NotEqual(a.Items[0].ComputeHash(), (a.Items[0] with { LotNo = "808640" }).ComputeHash());
        // header hash ignores items (items carry their own hash)
        Assert.Equal(a.ComputeHash(), (a with { Items = [] }).ComputeHash());
        // NULL and empty string are different source values
        Assert.NotEqual(BorrowSourceHash.Compute("a", null), BorrowSourceHash.Compute("a", ""));
        // trailing spaces from nchar/nvarchar padding do not change the hash
        Assert.Equal(BorrowSourceHash.Compute("CUB001"), BorrowSourceHash.Compute("CUB001  "));
    }

    [Fact]
    public void Plan_classifies_insert_update_delete_and_unchanged_for_bills_and_items()
    {
        var unchanged = Bill(1, "O1", items: Item(11, 1));
        var changed = Bill(2, "O2", "CUB001", null, Item(21, 2), Item(22, 2));
        var fresh = Bill(3, "O3", items: Item(31, 3));
        var snapshot = new[] { unchanged, changed, fresh };
        var mirror = new BorrowMirrorState(
            new Dictionary<int, string> { [1] = unchanged.ComputeHash(), [2] = "old-hash", [9] = "gone" },
            new Dictionary<int, string> { [11] = unchanged.Items[0].ComputeHash(), [21] = changed.Items[0].ComputeHash(), [22] = "stale", [99] = "orphan" });

        var plan = BorrowReconciliationPlan.Build(snapshot, mirror);

        Assert.Equal([3], plan.BillsToInsert.Select(b => b.SourceRecordNumber));
        Assert.Equal([2], plan.BillsToUpdate.Select(b => b.SourceRecordNumber));
        Assert.Equal([9], plan.BillsToDelete);
        Assert.Equal(1, plan.BillsUnchanged);
        Assert.Equal([31], plan.ItemsToInsert.Select(i => i.SourceRecordNumber));
        Assert.Equal([22], plan.ItemsToUpdate.Select(i => i.SourceRecordNumber));
        Assert.Equal([99], plan.ItemsToDelete);
        Assert.Equal(2, plan.ItemsUnchanged);
        Assert.True(plan.HasChanges);

        var result = BorrowSyncResult.From(plan, 3, 4, new DateTime(2026, 9, 17, 18, 0, 0));
        Assert.Equal(2, result.Inserted); Assert.Equal(2, result.Updated); Assert.Equal(2, result.Deleted);
    }

    [Fact]
    public void Plan_with_identical_mirror_has_no_changes_and_duplicate_source_ids_are_rejected()
    {
        var bill = Bill(1, items: Item(11, 1));
        var mirror = new BorrowMirrorState(new Dictionary<int, string> { [1] = bill.ComputeHash() }, new Dictionary<int, string> { [11] = bill.Items[0].ComputeHash() });
        Assert.False(BorrowReconciliationPlan.Build([bill], mirror).HasChanges);
        Assert.Throws<InvalidOperationException>(() => BorrowReconciliationPlan.Build([bill, bill], mirror));
    }

    [Fact]
    public void Overview_aggregates_by_facility_newest_first_with_counts_and_quantities()
    {
        var bills = new[]
        {
            Bill(1, "O1", "CUB001", new DateTime(2026, 8, 24), Item(11, 1, qty: 250), Item(12, 1, qty: 100)),
            Bill(2, "O2", "CUB001", new DateTime(2026, 7, 1), Item(21, 2, qty: 400)),
            Bill(3, "O3", "PAO001", new DateTime(2026, 3, 18), Item(31, 3, qty: 5)),
            Bill(4, "O4", "", new DateTime(2026, 9, 1), Item(41, 4, qty: 1)),   // blank facility code → "-"
        };
        var o = BorrowOverview.FromBills(bills);
        Assert.Equal(3, o.FacilityCount); Assert.Equal(4, o.BillCount); Assert.Equal(5, o.ItemCount); Assert.Equal(756m, o.TotalQty);
        Assert.Equal(["-", "CUB001", "PAO001"], o.Facilities.Select(f => f.FacilityCode));
        var cub = o.Facilities.Single(f => f.FacilityCode == "CUB001");
        Assert.Equal(2, cub.BillCount); Assert.Equal(3, cub.ItemCount); Assert.Equal(750m, cub.TotalQty); Assert.Equal(new DateTime(2026, 8, 24), cub.LatestDateReceive);
        Assert.Equal("ศูนย์สาธารณสุขและการแพทย์ตำบลทรายมูล", cub.DisplayName);
    }

    [Fact]
    public async Task Sync_reads_source_first_and_never_touches_mirror_when_source_read_fails()
    {
        var mirror = new RecordingMirror();
        var svc = new BorrowSyncService(new FailingSource(), mirror);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.SyncAsync());
        Assert.Equal(0, mirror.ApplyCalls);
        Assert.Equal(0, mirror.StateCalls);
    }

    [Fact]
    public async Task Sync_applies_plan_once_and_reports_counts()
    {
        var bill = Bill(113, items: Item(1036, 113));
        var mirror = new RecordingMirror();
        var svc = new BorrowSyncService(new FixedSource([bill]), mirror);
        var r = await svc.SyncAsync();
        Assert.Equal(1, mirror.ApplyCalls);
        Assert.Equal(1, r.BillsInserted); Assert.Equal(1, r.ItemsInserted); Assert.Equal(1, r.SourceBills); Assert.Equal(1, r.SourceItems);

        // second run against an up-to-date mirror: no apply, all unchanged
        mirror.Bills[113] = bill.ComputeHash(); mirror.Items[1036] = bill.Items[0].ComputeHash();
        var r2 = await svc.SyncAsync();
        Assert.Equal(1, mirror.ApplyCalls);
        Assert.Equal(1, r2.BillsUnchanged); Assert.Equal(1, r2.ItemsUnchanged); Assert.Equal(0, r2.Inserted + r2.Updated + r2.Deleted);
    }

    private sealed class FailingSource : IBorrowSourceRepository
    {
        public Task<IReadOnlyList<BorrowSourceBill>> GetSnapshotAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException("INV unreachable");
    }

    private sealed class FixedSource(IReadOnlyList<BorrowSourceBill> bills) : IBorrowSourceRepository
    {
        public Task<IReadOnlyList<BorrowSourceBill>> GetSnapshotAsync(CancellationToken cancellationToken = default) => Task.FromResult(bills);
    }

    private sealed class RecordingMirror : IBorrowMirrorRepository
    {
        public Dictionary<int, string> Bills { get; } = [];
        public Dictionary<int, string> Items { get; } = [];
        public int ApplyCalls; public int StateCalls;
        public Task<BorrowMirrorState> GetStateAsync(CancellationToken cancellationToken = default) { StateCalls++; return Task.FromResult(new BorrowMirrorState(Bills, Items)); }
        public Task ApplyAsync(BorrowReconciliationPlan plan, DateTime syncedAt, CancellationToken cancellationToken = default) { ApplyCalls++; return Task.CompletedTask; }
        public Task<IReadOnlyList<BorrowSourceBill>> GetBillsAsync(string? facilityCode, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<BorrowSourceBill>>([]);
    }
}
