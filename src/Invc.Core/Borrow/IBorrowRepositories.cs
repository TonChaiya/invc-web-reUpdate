namespace Invc.Core.Borrow;

/// <summary>Reads the COMPLETE current type-09 snapshot from SQL Server INV. SELECT only.</summary>
public interface IBorrowSourceRepository
{
    /// <summary>All type-09 headers with their lines, facility names and drug names resolved. Never partial.</summary>
    Task<IReadOnlyList<BorrowSourceBill>> GetSnapshotAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Application-owned mirror in MySQL (`invc_web`). This is the only writable store in the application;
/// it must never point at SQL Server INV (enforced by the MySQL connection factory).
/// </summary>
public interface IBorrowMirrorRepository
{
    /// <summary>Stored hashes of every mirrored bill/item (for the reconciliation plan).</summary>
    Task<BorrowMirrorState> GetStateAsync(CancellationToken cancellationToken = default);

    /// <summary>Applies the plan inside ONE transaction; any failure rolls everything back.</summary>
    Task ApplyAsync(BorrowReconciliationPlan plan, DateTime syncedAt, CancellationToken cancellationToken = default);

    /// <summary>All mirrored bills with items (newest DATE_RECEIVE first), optionally limited to one facility.</summary>
    Task<IReadOnlyList<BorrowSourceBill>> GetBillsAsync(string? facilityCode, CancellationToken cancellationToken = default);
}

/// <summary>
/// Automatic reconciliation: (1) read the full snapshot from INV — if that fails nothing in the mirror is touched;
/// (2) diff against the mirror's stored hashes; (3) apply inserts/updates/deletes in one MySQL transaction.
/// </summary>
public sealed class BorrowSyncService(IBorrowSourceRepository source, IBorrowMirrorRepository mirror, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public async Task<BorrowSyncResult> SyncAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await source.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);   // throws → mirror untouched
        var state = await mirror.GetStateAsync(cancellationToken).ConfigureAwait(false);
        var plan = BorrowReconciliationPlan.Build(snapshot, state);
        var now = _clock.GetLocalNow().DateTime;
        if (plan.HasChanges)
        {
            await mirror.ApplyAsync(plan, now, cancellationToken).ConfigureAwait(false);
        }
        return BorrowSyncResult.From(plan, snapshot.Count, snapshot.Sum(b => b.Items.Count), now);
    }
}
