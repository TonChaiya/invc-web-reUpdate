using Dapper;
using Invc.Core.Dashboard;
using Invc.Core.Inventory;
using Invc.Infrastructure.Data;
using Invc.Infrastructure.Receipts;

namespace Invc.Infrastructure.Dashboard;

/// <summary>
/// Read-only SQL for the legacy dashboard KPIs that have no domain owner (default.asp / Dashboard.asp / table_*.asp
/// semantics, docs/legacy-query-map.md). Inventory, Reorder, PO and Receipt figures are NOT queried here — the
/// dashboard reuses those modules. Fiscal-year windows are typed [1 Oct, 1 Oct) parameters; no string dates.
/// </summary>
internal static class DashboardSql
{
    /// <summary>D1 — Dashboard.asp: SELECT [year], SUM(money) FROM BUDGET WHERE [year] = @Y GROUP BY [year].</summary>
    public const string Budget = """
        SELECT COUNT(*) AS BudgetRows, SUM(money) AS TotalMoney FROM dbo.BUDGET WHERE [year] = @Year
        """;

    /// <summary>D4 — SUM(SUBSTOCK.TOTAL_VALUE); department names via LEFT JOIN (DEPT_ID is the PK of DEPT_ID).</summary>
    public const string Substock = """
        SELECT s.DEPT_ID AS DeptId, d.DEPT_NAME AS DeptName, COUNT(*) AS ItemCount, ISNULL(SUM(s.TOTAL_VALUE), 0) AS TotalValue
        FROM dbo.SUBSTOCK s
        LEFT JOIN dbo.DEPT_ID d ON d.DEPT_ID = s.DEPT_ID
        GROUP BY s.DEPT_ID, d.DEPT_NAME
        ORDER BY s.DEPT_ID
        """;

    /// <summary>D5 — latest MBS_RE_M YEAR/MONTH with Σ SALE_VALUE and the MNTH_SUM Σ TOTAL_VALUE for the same period.</summary>
    public const string StockCoverage = """
        SELECT TOP 1 r.[YEAR] AS [Year], r.[MONTH] AS [Month],
               (SELECT SUM(m.TOTAL_VALUE) FROM dbo.MNTH_SUM m WHERE m.[YEAR] = r.[YEAR] AND m.[MONTH] = r.[MONTH]) AS MonthEndValue,
               SUM(r.SALE_VALUE) AS SaleValue
        FROM dbo.MBS_RE_M r
        GROUP BY r.[YEAR], r.[MONTH]
        ORDER BY r.[YEAR] DESC, r.[MONTH] DESC
        """;

    /// <summary>D6 — default.asp Countdrug: NOUSE, OUT_OF_LIST and PO_INDIVIDUAL all NULL, grouped by ED_NED; name via LEFT JOIN.</summary>
    public const string LegacyEdNed = """
        SELECT m.ED_NED AS Code, e.EDNAME AS Name, e.EDMAP AS ShortName, COUNT(*) AS ItemCount, ISNULL(SUM(m.TOTAL_VALUE), 0) AS TotalValue
        FROM dbo.INV_MD m
        LEFT JOIN dbo.TBLED_NED e ON e.EDCODE = m.ED_NED
        WHERE m.NOUSE IS NULL AND m.OUT_OF_LIST IS NULL AND m.PO_INDIVIDUAL IS NULL
        GROUP BY m.ED_NED, e.EDNAME, e.EDMAP
        ORDER BY m.ED_NED
        """;

    /// <summary>D7 — Dashboard.asp active agreements (BuyQty &lt; AgreeQty AND Expdate &gt; today); value is computed in Core.</summary>
    public const string ActiveAgreements = """
        SELECT CAST(a.AgreeQty AS decimal(18, 4)) AS AgreeQty, CAST(a.BuyQty AS decimal(18, 4)) AS BuyQty, CAST(a.PACK_RATIO AS decimal(18, 4)) AS PackRatio, CAST(a.UnitPrice AS decimal(18, 4)) AS UnitPrice
        FROM dbo.Agreement a
        WHERE a.BuyQty < a.AgreeQty AND a.Expdate > @Today
        """;

    /// <summary>
    /// Processed monthly report (2026-09-18, replaces the CARD-based D9 summary): MNTH_SUM = complete end-of-month snapshot
    /// (QTY_REMAIN / TOTAL_VALUE), keyed by CE YEAR + 2-char MONTH, aggregated by the HISTORICAL ED_NED recorded at processing
    /// time (name from TBLED_NED via OUTER APPLY TOP 1). The window is passed as Buddhist-free CE 'yyyymm' text keys and covers the
    /// fiscal year plus the immediately preceding calendar month (opening source of the first FY month only).
    /// Live audit: (YEAR, MONTH, WORKING_CODE) is unique in both tables; MBS_RE_M.REMAIN_* is never read (movement-only rows).
    /// MBS_RE_Y is deliberately unused (only one year exists, totals do not reconcile — UNRESOLVED).
    /// </summary>
    public const string ProcessedSnapshots = """
        SELECT CAST(RTRIM(s.YEAR) AS int) AS Year,
               CAST(RTRIM(s.MONTH) AS int) AS Month,
               RTRIM(s.ED_NED) AS EdNed,
               t.EdNedName,
               COUNT(*) AS ItemCount,
               ISNULL(SUM(s.QTY_REMAIN), 0) AS QtyRemain,
               ISNULL(SUM(s.TOTAL_VALUE), 0) AS TotalValue
        FROM dbo.MNTH_SUM s
        OUTER APPLY (SELECT TOP 1 RTRIM(x.EDNAME) AS EdNedName FROM dbo.TBLED_NED x WHERE RTRIM(x.EDCODE) = RTRIM(s.ED_NED) ORDER BY x.EDCODE) t
        WHERE RTRIM(s.YEAR) + RIGHT('0' + RTRIM(s.MONTH), 2) >= @FromKey
          AND RTRIM(s.YEAR) + RIGHT('0' + RTRIM(s.MONTH), 2) <= @ToKey
        GROUP BY RTRIM(s.YEAR), RTRIM(s.MONTH), RTRIM(s.ED_NED), t.EdNedName
        ORDER BY RTRIM(s.YEAR) DESC, RTRIM(s.MONTH) DESC, RTRIM(s.ED_NED)
        """;

    /// <summary>MBS_RE_M monthly receive / issue flow per (YEAR, MONTH, historical ED_NED) for the fiscal year window.</summary>
    public const string ProcessedFlows = """
        SELECT CAST(RTRIM(m.YEAR) AS int) AS Year,
               CAST(RTRIM(m.MONTH) AS int) AS Month,
               RTRIM(m.ED_NED) AS EdNed,
               t.EdNedName,
               COUNT(*) AS ItemCount,
               ISNULL(SUM(m.RCV_QUAN), 0) AS RcvQuan,
               ISNULL(SUM(m.RCV_VALUE), 0) AS RcvValue,
               ISNULL(SUM(m.SALE_QUAN), 0) AS SaleQuan,
               ISNULL(SUM(m.SALE_VALUE), 0) AS SaleValue
        FROM dbo.MBS_RE_M m
        OUTER APPLY (SELECT TOP 1 RTRIM(x.EDNAME) AS EdNedName FROM dbo.TBLED_NED x WHERE RTRIM(x.EDCODE) = RTRIM(m.ED_NED) ORDER BY x.EDCODE) t
        WHERE RTRIM(m.YEAR) + RIGHT('0' + RTRIM(m.MONTH), 2) >= @FromKey
          AND RTRIM(m.YEAR) + RIGHT('0' + RTRIM(m.MONTH), 2) <= @ToKey
        GROUP BY RTRIM(m.YEAR), RTRIM(m.MONTH), RTRIM(m.ED_NED), t.EdNedName
        ORDER BY RTRIM(m.YEAR) DESC, RTRIM(m.MONTH) DESC, RTRIM(m.ED_NED)
        """;

    /// <summary>C11 — Dashboard.asp / ipiss_process.asp process time per PO month (fiscal year by PO_NO prefix, as Phase 4).</summary>
    public const string ProcessTime = """
        SELECT LEFT(CONVERT(varchar(8), p.PO_DATE, 112), 6) AS PoMonth,
               COUNT(*) AS PoCount,
               AVG(CAST(DATEDIFF(DAY, p.PO_DATE, p.BILLIN) AS decimal(10, 2)))  AS SendDays,
               AVG(CAST(DATEDIFF(DAY, p.BILLIN, p.BILLOUT) AS decimal(10, 2)))  AS DocDays,
               AVG(CAST(DATEDIFF(DAY, p.BILLOUT, p.BILLEND) AS decimal(10, 2))) AS AccDays
        FROM dbo.MS_PO p
        WHERE p.PO_NO LIKE @PrefixPattern AND p.PO_DATE IS NOT NULL
        GROUP BY LEFT(CONVERT(varchar(8), p.PO_DATE, 112), 6)
        ORDER BY LEFT(CONVERT(varchar(8), p.PO_DATE, 112), 6) DESC
        """;

    /// <summary>D10 header — item identity for the trend (no hard-coded WORKING_CODE).</summary>
    public const string ItemHeader = """
        SELECT TOP 1 m.WORKING_CODE AS WorkingCode, m.DRUG_NAME AS DrugName, m.QTY_ON_HAND AS QtyOnHand, m.SALE_UNIT AS SaleUnit
        FROM dbo.INV_MD m WHERE m.WORKING_CODE = @WorkingCode ORDER BY m.RECORD_NUMBER
        """;

    /// <summary>D10 — table_by_item.asp: CARD issues (R_S_STATUS = 'S') per Thai month for one item, newest first.</summary>
    public const string ItemTrend = """
        SELECT CAST(YEAR(c.OPERATE_DATE) + 543 AS varchar(4)) + RIGHT('0' + CAST(MONTH(c.OPERATE_DATE) AS varchar(2)), 2) AS MonthKey,
               ISNULL(SUM(ISNULL(c.ACTIVE_QTY1, 0) + ISNULL(c.ACTIVE_QTY2, 0) + ISNULL(c.ACTIVE_QTY3, 0)), 0) AS SaleQuantity,
               ISNULL(SUM(c.[VALUE]), 0) AS SaleValue
        FROM dbo.CARD c
        WHERE c.WORKING_CODE = @WorkingCode AND c.R_S_STATUS = 'S' AND c.OPERATE_DATE >= @From
        GROUP BY YEAR(c.OPERATE_DATE), MONTH(c.OPERATE_DATE)
        ORDER BY YEAR(c.OPERATE_DATE) DESC, MONTH(c.OPERATE_DATE) DESC
        """;

    public static IEnumerable<string> AllStatements()
    {
        yield return Budget; yield return Substock; yield return StockCoverage; yield return LegacyEdNed;
        yield return ActiveAgreements; yield return ProcessedSnapshots; yield return ProcessedFlows; yield return ProcessTime; yield return ItemHeader; yield return ItemTrend;
    }
}

public sealed class DashboardAnalyticsRepository(ISqlConnectionFactory connections) : IDashboardAnalyticsRepository
{
    public async Task<DashboardBudget> GetBudgetAsync(int fiscalYear, CancellationToken ct = default)
    {
        await using var c = await connections.OpenAsync(ct).ConfigureAwait(false);
        var row = await c.QuerySingleAsync<(int BudgetRows, decimal? TotalMoney)>(Cmd(DashboardSql.Budget,
            new { Year = new DbString { Value = fiscalYear.ToString(), IsAnsi = false, Length = 4 } }, ct)).ConfigureAwait(false);
        return new DashboardBudget(fiscalYear, row.BudgetRows, row.TotalMoney);
    }

    public async Task<DashboardSubstock> GetSubstockAsync(CancellationToken ct = default)
    {
        await using var c = await connections.OpenAsync(ct).ConfigureAwait(false);
        var rows = (await c.QueryAsync<DashboardSubstockRow>(Cmd(DashboardSql.Substock, null, ct)).ConfigureAwait(false)).ToList();
        return new DashboardSubstock(rows.Sum(r => r.TotalValue), rows.Sum(r => r.ItemCount), rows);
    }

    public async Task<DashboardStockCoverage> GetStockCoverageAsync(CancellationToken ct = default)
    {
        await using var c = await connections.OpenAsync(ct).ConfigureAwait(false);
        var row = await c.QuerySingleOrDefaultAsync<DashboardStockCoverage>(Cmd(DashboardSql.StockCoverage, null, ct)).ConfigureAwait(false);
        return row ?? new DashboardStockCoverage(null, null, null, null);
    }

    public async Task<IReadOnlyList<DashboardEdNedRow>> GetLegacyEdNedAsync(CancellationToken ct = default)
    {
        await using var c = await connections.OpenAsync(ct).ConfigureAwait(false);
        return (await c.QueryAsync<DashboardEdNedRow>(Cmd(DashboardSql.LegacyEdNed, null, ct)).ConfigureAwait(false)).AsList();
    }

    public async Task<IReadOnlyList<DashboardAgreementRow>> GetActiveAgreementsAsync(DateTime today, CancellationToken ct = default)
    {
        await using var c = await connections.OpenAsync(ct).ConfigureAwait(false);
        return (await c.QueryAsync<DashboardAgreementRow>(Cmd(DashboardSql.ActiveAgreements, new { Today = today }, ct)).ConfigureAwait(false)).AsList();
    }

    public async Task<IReadOnlyList<DashboardProcessedSnapshotRow>> GetProcessedSnapshotsAsync(int fiscalYear, CancellationToken ct = default)
    {
        var (fromKey, toKey) = ProcessedWindow(fiscalYear, includePrecedingMonth: true);
        await using var c = await connections.OpenAsync(ct).ConfigureAwait(false);
        return (await c.QueryAsync<DashboardProcessedSnapshotRow>(Cmd(DashboardSql.ProcessedSnapshots, new { FromKey = fromKey, ToKey = toKey }, ct)).ConfigureAwait(false)).AsList();
    }

    public async Task<IReadOnlyList<DashboardProcessedFlowRow>> GetProcessedFlowsAsync(int fiscalYear, CancellationToken ct = default)
    {
        var (fromKey, toKey) = ProcessedWindow(fiscalYear, includePrecedingMonth: false);
        await using var c = await connections.OpenAsync(ct).ConfigureAwait(false);
        return (await c.QueryAsync<DashboardProcessedFlowRow>(Cmd(DashboardSql.ProcessedFlows, new { FromKey = fromKey, ToKey = toKey }, ct)).ConfigureAwait(false)).AsList();
    }

    /// <summary>
    /// CE 'yyyymm' text window for a Thai fiscal year: Oct (FY−544) … Sep (FY−543); with the preceding month, from Sep (FY−544).
    /// MNTH_SUM / MBS_RE_M store CE years, so the Buddhist FY is converted here — never treated as a Buddhist YEAR column.
    /// </summary>
    internal static (string FromKey, string ToKey) ProcessedWindow(int buddhistFiscalYear, bool includePrecedingMonth)
    {
        var ceStart = buddhistFiscalYear - Core.Common.ThaiFiscalYear.BuddhistEraOffset - 1;
        var from = includePrecedingMonth ? $"{ceStart}09" : $"{ceStart}10";
        var to = $"{ceStart + 1}09";
        return (from, to);
    }

    public async Task<IReadOnlyList<DashboardProcessTimeRow>> GetProcessTimeAsync(int fiscalYear, CancellationToken ct = default)
    {
        await using var c = await connections.OpenAsync(ct).ConfigureAwait(false);
        var prefix = new DbString { Value = Core.PurchaseOrders.PurchaseOrderRules.PoPrefix(fiscalYear) + "%", IsAnsi = false, Length = 10 };
        return (await c.QueryAsync<DashboardProcessTimeRow>(Cmd(DashboardSql.ProcessTime, new { PrefixPattern = prefix }, ct)).ConfigureAwait(false)).AsList();
    }

    public async Task<DashboardItemTrend?> GetItemTrendAsync(string workingCode, int months, CancellationToken ct = default)
    {
        if (DashboardRules.NormalizeItemCode(workingCode) is null)
        {
            return null;
        }

        var code = new DbString { Value = workingCode, IsAnsi = false, Length = 7 };
        var from = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-(months - 1));
        await using var c = await connections.OpenAsync(ct).ConfigureAwait(false);
        var header = await c.QuerySingleOrDefaultAsync<(string WorkingCode, string? DrugName, decimal? QtyOnHand, string? SaleUnit)>(
            Cmd(DashboardSql.ItemHeader, new { WorkingCode = code }, ct)).ConfigureAwait(false);
        if (header.WorkingCode is null)
        {
            return null;
        }

        var rows = (await c.QueryAsync<DashboardItemTrendRow>(Cmd(DashboardSql.ItemTrend, new { WorkingCode = code, From = from }, ct)).ConfigureAwait(false)).AsList();
        return new DashboardItemTrend(header.WorkingCode, header.DrugName, header.QtyOnHand, header.SaleUnit, rows);
    }

    private CommandDefinition Cmd(string sql, object? parameters, CancellationToken ct)
        => new(ReadOnlySql.Ensure(sql), parameters, commandTimeout: connections.CommandTimeoutSeconds, cancellationToken: ct);
}
