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

    /// <summary>D9 — table_monthly_rpt.asp: CARD grouped by Thai month and R_S_STATUS + LEFT(R_S_NUMBER,1) for the FY window.</summary>
    public const string Movement = """
        SELECT CAST(YEAR(c.OPERATE_DATE) + 543 AS varchar(4)) + RIGHT('0' + CAST(MONTH(c.OPERATE_DATE) AS varchar(2)), 2) AS MonthKey,
               c.R_S_STATUS AS Status,
               LEFT(c.R_S_NUMBER, 1) AS NumberPrefix,
               COUNT(*) AS [Count],
               ISNULL(SUM(c.[VALUE]), 0) AS Value,
               ISNULL(SUM(ISNULL(c.ACTIVE_QTY1, 0) + ISNULL(c.ACTIVE_QTY2, 0) + ISNULL(c.ACTIVE_QTY3, 0)), 0) AS Quantity
        FROM dbo.CARD c
        WHERE c.OPERATE_DATE >= @From AND c.OPERATE_DATE < @To
        GROUP BY YEAR(c.OPERATE_DATE), MONTH(c.OPERATE_DATE), c.R_S_STATUS, LEFT(c.R_S_NUMBER, 1)
        ORDER BY YEAR(c.OPERATE_DATE) DESC, MONTH(c.OPERATE_DATE) DESC, c.R_S_STATUS, LEFT(c.R_S_NUMBER, 1)
        """;

    /// <summary>
    /// D9 by item type (2026-09-18): the same CARD window grouped additionally by INV_MD.ED_NED → TBLED_NED. Lookups are
    /// OUTER APPLY TOP 1 (deterministic, never multiply CARD rows; audit FY2569: 1958 = 1958 rows); rows whose item or type
    /// cannot be resolved keep a NULL type and are surfaced as "ไม่ระบุประเภท" in Core. No LOCATION / GROUP_CODE involved.
    /// </summary>
    public const string MovementByItemType = """
        SELECT CAST(YEAR(c.OPERATE_DATE) + 543 AS varchar(4)) + RIGHT('0' + CAST(MONTH(c.OPERATE_DATE) AS varchar(2)), 2) AS MonthKey,
               c.R_S_STATUS AS Status,
               LEFT(c.R_S_NUMBER, 1) AS NumberPrefix,
               md.ItemTypeCode,
               t.ItemTypeName,
               COUNT(*) AS [Count],
               ISNULL(SUM(c.[VALUE]), 0) AS Value,
               ISNULL(SUM(ISNULL(c.ACTIVE_QTY1, 0) + ISNULL(c.ACTIVE_QTY2, 0) + ISNULL(c.ACTIVE_QTY3, 0)), 0) AS Quantity
        FROM dbo.CARD c
        OUTER APPLY (SELECT TOP 1 RTRIM(m.ED_NED) AS ItemTypeCode FROM dbo.INV_MD m WHERE m.WORKING_CODE = c.WORKING_CODE ORDER BY m.RECORD_NUMBER) md
        OUTER APPLY (SELECT TOP 1 RTRIM(x.EDNAME) AS ItemTypeName FROM dbo.TBLED_NED x WHERE x.EDCODE = md.ItemTypeCode ORDER BY x.EDCODE) t
        WHERE c.OPERATE_DATE >= @From AND c.OPERATE_DATE < @To
        GROUP BY YEAR(c.OPERATE_DATE), MONTH(c.OPERATE_DATE), c.R_S_STATUS, LEFT(c.R_S_NUMBER, 1), md.ItemTypeCode, t.ItemTypeName
        ORDER BY YEAR(c.OPERATE_DATE) DESC, MONTH(c.OPERATE_DATE) DESC, md.ItemTypeCode, c.R_S_STATUS, LEFT(c.R_S_NUMBER, 1)
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
        yield return ActiveAgreements; yield return Movement; yield return MovementByItemType; yield return ProcessTime; yield return ItemHeader; yield return ItemTrend;
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

    public async Task<IReadOnlyList<DashboardMovementRow>> GetMovementAsync(int fiscalYear, CancellationToken ct = default)
    {
        var (from, to) = NonPoReceiptRepository.FiscalYearWindow(fiscalYear);
        await using var c = await connections.OpenAsync(ct).ConfigureAwait(false);
        return (await c.QueryAsync<DashboardMovementRow>(Cmd(DashboardSql.Movement, new { From = from, To = to }, ct)).ConfigureAwait(false)).AsList();
    }

    public async Task<IReadOnlyList<DashboardMovementItemTypeRow>> GetMovementByItemTypeAsync(int fiscalYear, CancellationToken ct = default)
    {
        var (from, to) = NonPoReceiptRepository.FiscalYearWindow(fiscalYear);
        await using var c = await connections.OpenAsync(ct).ConfigureAwait(false);
        return (await c.QueryAsync<DashboardMovementItemTypeRow>(Cmd(DashboardSql.MovementByItemType, new { From = from, To = to }, ct)).ConfigureAwait(false)).AsList();
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
