using Dapper;
using Invc.Infrastructure.Data;
using Invc.Infrastructure.Inventory;

namespace Invc.IntegrationTests;

/// <summary>
/// Parity checks A1–A3/A9 from docs/report-parity-matrix.md: the new repository must return
/// exactly what the legacy INV_Status.asp SQL returns, executed at the same moment.
/// </summary>
public class InventoryParityTests(ProductionReadOnlyFixture db) : IClassFixture<ProductionReadOnlyFixture>
{
    [SkippableFact]
    public async Task Status_row_count_matches_legacy_query()
    {
        var connections = db.RequireOrSkip();
        var repo = new InventoryRepository(connections);

        await using var c = await connections.OpenAsync();
        var legacyCount = await c.ExecuteScalarAsync<int>(ReadOnlySql.Ensure(
            "SELECT COUNT(*) FROM dbo.INV_MD WHERE [NOUSE] IS NULL"));

        var items = await repo.GetStatusAsync(null);
        Assert.Equal(legacyCount, items.Count);
    }

    [SkippableFact]
    public async Task Status_order_matches_legacy_numeric_order()
    {
        var connections = db.RequireOrSkip();
        var repo = new InventoryRepository(connections);

        await using var c = await connections.OpenAsync();
        // Legacy ORDER BY CAST(WORKING_CODE AS INT); guard against non-numeric codes so the probe itself cannot fail.
        var legacyOrder = (await c.QueryAsync<string>(ReadOnlySql.Ensure("""
            SELECT WORKING_CODE FROM dbo.INV_MD
            WHERE [NOUSE] IS NULL AND WORKING_CODE NOT LIKE '%[^0-9]%'
            ORDER BY CAST(WORKING_CODE AS int) ASC, DRUG_NAME COLLATE Thai_CI_AS ASC
            """))).ToList();

        var items = await repo.GetStatusAsync(null);
        var newOrder = items.Where(i => i.WorkingCode.All(char.IsDigit)).Select(i => i.WorkingCode).ToList();

        Assert.Equal(legacyOrder, newOrder);
    }

    [SkippableFact]
    public async Task Summary_matches_legacy_aggregates()
    {
        var connections = db.RequireOrSkip();
        var repo = new InventoryRepository(connections);

        await using var c = await connections.OpenAsync();
        var legacy = await c.QuerySingleAsync<(int Count, decimal Qty, decimal Value)>(ReadOnlySql.Ensure(
            "SELECT COUNT(*), ISNULL(SUM(QTY_ON_HAND),0), ISNULL(SUM(TOTAL_VALUE),0) FROM dbo.INV_MD WHERE [NOUSE] IS NULL"));

        var summary = await repo.GetSummaryAsync();
        Assert.Equal(legacy.Count, summary.ActiveItemCount);
        Assert.Equal(legacy.Qty, summary.TotalQtyOnHand);
        Assert.Equal(legacy.Value, summary.TotalValue);
    }

    [SkippableFact]
    public async Task Keyword_search_matches_legacy_like_filter()
    {
        var connections = db.RequireOrSkip();
        var repo = new InventoryRepository(connections);
        const string keyword = "10";

        await using var c = await connections.OpenAsync();
        var legacyCount = await c.ExecuteScalarAsync<int>(ReadOnlySql.Ensure("""
            SELECT COUNT(*) FROM dbo.INV_MD WHERE (NOUSE IS NULL) AND (
                 [drug_name]    LIKE @P OR [composition] LIKE @P OR [HOSP_CODE] LIKE @P OR [WORKING_CODE] LIKE @P)
            """), new { P = $"%{keyword}%" });

        var items = await repo.GetStatusAsync(keyword);
        Assert.Equal(legacyCount, items.Count);
    }
}
