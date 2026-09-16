using Invc.Core.Common;
using Invc.Core.Inventory;
using Invc.Core.PurchaseOrders;
using Invc.Core.Receipts;
using Invc.Core.Reorder;

namespace Invc.Core.Dashboard;

/// <summary>
/// Dashboard-only rules for the legacy KPIs that have no domain owner (default.asp / Dashboard.asp semantics).
/// Everything else on the dashboard comes from the trusted module reports (Inventory, Reorder, PO, Receipts).
/// </summary>
public static class DashboardRules
{
    public static int? TryParseFiscalYear(string? value)
        => int.TryParse(value?.Trim(), out var y) && y is >= 2500 and <= 2599 ? y : null;

    public static int FiscalYearOf(DateTime date) => ThaiFiscalYear.FromDate(date);

    /// <summary>Legacy month key: yyyymm + 54300 (e.g. 2026-09 → 256909), as used by table_monthly_rpt.asp.</summary>
    public static string ThaiMonthKey(DateTime date) => ThaiFiscalYear.ToBuddhistYearMonth(DateOnly.FromDateTime(date));

    /// <summary>D5: SUM(MNTH_SUM.TOTAL_VALUE) / SUM(MBS_RE_M.SALE_VALUE) for the latest month; null when undefined.</summary>
    public static decimal? StockCoverage(decimal? monthEndValue, decimal? saleValue)
        => monthEndValue is { } m && saleValue is > 0m ? m / saleValue.Value : null;

    /// <summary>D7: (AgreeQty − BuyQty) / PACK_RATIO × UnitPrice; null when the ratio is 0/NULL or key inputs are missing.</summary>
    public static decimal? AgreementRemaining(decimal? agreeQty, decimal? buyQty, decimal? packRatio, decimal? unitPrice)
        => agreeQty is { } a && packRatio is > 0m && unitPrice is { } p ? (a - (buyQty ?? 0m)) / packRatio.Value * p : null;

    /// <summary>D10 item selector: INV_MD.WORKING_CODE rules (1–7 letters/digits).</summary>
    public static string? NormalizeItemCode(string? raw)
    {
        var t = raw?.Trim();
        return t is { Length: > 0 and <= 7 } && t.All(char.IsLetterOrDigit) ? t : null;
    }
}

public enum MovementDirection { Receive, Issue, Other }

/// <summary>Legacy CARD category = R_S_STATUS + LEFT(R_S_NUMBER,1). Raw key preserved; direction is a display aid only.</summary>
public sealed record MovementCategory(string Key, MovementDirection Direction, string ThaiLabel)
{
    public static MovementCategory FromRaw(string? status, string? number)
    {
        var s = string.IsNullOrEmpty(status) ? "?" : status.Trim().ToUpperInvariant();
        var p = string.IsNullOrEmpty(number) ? "?" : number.Substring(0, 1).ToUpperInvariant();
        var direction = s switch { "R" => MovementDirection.Receive, "S" => MovementDirection.Issue, _ => MovementDirection.Other };
        var label = (s, p) switch
        {
            ("R", "O") => "รับเข้า (ใบรับอื่น/CUP)",
            ("R", "S") => "รับคืนจากคลังย่อย",
            ("R", _) => "รับเข้า",
            ("S", "S") => "จ่ายให้คลังย่อย",
            ("S", "O") => "จ่ายออกอื่น",
            ("S", _) => "จ่ายออก",
            _ => "อื่น ๆ",
        };
        return new MovementCategory(s + p, direction, label);
    }
}

/// <summary>Raw aggregate row from CARD: one month × one category.</summary>
public sealed record DashboardMovementRow(string MonthKey, string? Status, string? NumberPrefix, int Count, decimal Value, decimal Quantity);

public sealed record DashboardMovementCategory(MovementCategory Category, int Count, decimal Value, decimal Quantity);

public sealed record DashboardMovementMonth(string MonthKey, IReadOnlyList<DashboardMovementCategory> Categories)
{
    public decimal ReceiveValue => Categories.Where(c => c.Category.Direction == MovementDirection.Receive).Sum(c => c.Value);
    public decimal IssueValue => Categories.Where(c => c.Category.Direction == MovementDirection.Issue).Sum(c => c.Value);
    public decimal OtherValue => Categories.Where(c => c.Category.Direction == MovementDirection.Other).Sum(c => c.Value);
    public int Count => Categories.Sum(c => c.Count);

    /// <summary>256810 → "ต.ค. 2568".</summary>
    public string DisplayMonth => MonthKey.Length == 6 && int.TryParse(MonthKey[4..], out var m) && m is >= 1 and <= 12
        ? $"{ThaiMonths[m - 1]} {MonthKey[..4]}"
        : MonthKey;

    internal static readonly string[] ThaiMonths = ["ม.ค.", "ก.พ.", "มี.ค.", "เม.ย.", "พ.ค.", "มิ.ย.", "ก.ค.", "ส.ค.", "ก.ย.", "ต.ค.", "พ.ย.", "ธ.ค."];
}

public static class DashboardMovement
{
    /// <summary>Groups raw rows by month (newest first); every raw category is retained under its month.</summary>
    public static IReadOnlyList<DashboardMovementMonth> Build(IEnumerable<DashboardMovementRow> rows)
        => rows.GroupBy(r => r.MonthKey)
            .OrderByDescending(g => g.Key, StringComparer.Ordinal)
            .Select(g => new DashboardMovementMonth(g.Key,
                g.GroupBy(r => MovementCategory.FromRaw(r.Status, r.NumberPrefix))
                 .Select(c => new DashboardMovementCategory(c.Key, c.Sum(r => r.Count), c.Sum(r => r.Value), c.Sum(r => r.Quantity)))
                 .OrderBy(c => c.Category.Key, StringComparer.Ordinal).ToList()))
            .ToList();
}

/// <summary>D1: BUDGET rows for the selected year (legacy SUM(money)).</summary>
public sealed record DashboardBudget(int FiscalYear, int RowCount, decimal? TotalMoney)
{
    public bool HasBudget => RowCount > 0 && TotalMoney.HasValue;
}

/// <summary>D4: SUBSTOCK totals; department names via safe lookup.</summary>
public sealed record DashboardSubstockRow(string? DeptId, string? DeptName, int ItemCount, decimal TotalValue)
{
    public string DisplayName => string.IsNullOrWhiteSpace(DeptName) ? $"{DeptId ?? "–"} (ไม่พบชื่อหน่วย)" : DeptName!;
}

public sealed record DashboardSubstock(decimal TotalValue, int RowCount, IReadOnlyList<DashboardSubstockRow> ByDepartment);

/// <summary>D5: latest MBS_RE_M period with its MNTH_SUM value and SALE_VALUE.</summary>
public sealed record DashboardStockCoverage(string? Year, string? Month, decimal? MonthEndValue, decimal? SaleValue)
{
    public bool HasPeriod => !string.IsNullOrEmpty(Year) && !string.IsNullOrEmpty(Month);
    public decimal? Ratio => DashboardRules.StockCoverage(MonthEndValue, SaleValue);
    public string DisplayPeriod => HasPeriod && int.TryParse(Month, out var m) && m is >= 1 and <= 12 && int.TryParse(Year, out var y)
        ? $"{DashboardMovementMonth.ThaiMonths[m - 1]} {y + ThaiFiscalYear.BuddhistEraOffset}"
        : "–";
}

/// <summary>D6: legacy ED/NED item count & value (NOUSE, OUT_OF_LIST, PO_INDIVIDUAL all NULL).</summary>
public sealed record DashboardEdNedRow(string? Code, string? Name, string? ShortName, int ItemCount, decimal TotalValue)
{
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? $"รหัส {Code ?? "?"}" : Name!;
}

/// <summary>D7: active agreements (BuyQty &lt; AgreeQty AND Expdate &gt; today).</summary>
public sealed record DashboardAgreements(int ActiveCount, decimal RemainingValue, int UnvaluableCount)
{
    public bool HasActiveAgreements => ActiveCount > 0;
}

/// <summary>Raw active-agreement row used to compute D7 in Core.</summary>
public sealed record DashboardAgreementRow(decimal? AgreeQty, decimal? BuyQty, decimal? PackRatio, decimal? UnitPrice);

/// <summary>C11: monthly average process days (PO_DATE→BILLIN, BILLIN→BILLOUT, BILLOUT→BILLEND); nulls mean no data.</summary>
public sealed record DashboardProcessTimeRow(string PoMonth, int PoCount, decimal? SendDays, decimal? DocDays, decimal? AccDays)
{
    public bool HasAnyValue => SendDays.HasValue || DocDays.HasValue || AccDays.HasValue;
}

/// <summary>D10: monthly CARD issue history for one item.</summary>
public sealed record DashboardItemTrendRow(string MonthKey, decimal SaleQuantity, decimal SaleValue);

public sealed record DashboardItemTrend(string WorkingCode, string? DrugName, decimal? QtyOnHand, string? SaleUnit, IReadOnlyList<DashboardItemTrendRow> Months);

/// <summary>One dashboard section: either a value or a user-safe failure message (never a stack trace).</summary>
public sealed record DashboardSection<T>(bool IsAvailable, T? Value, string? Error)
{
    public static DashboardSection<T> Ok(T value) => new(true, value, null);
    public static DashboardSection<T> Failed(string error) => new(false, default, error);
}

public sealed record DashboardSnapshot
{
    public required int FiscalYear { get; init; }
    public string? FiscalYearNote { get; init; }
    public IReadOnlyList<int> FiscalYearOptions { get; init; } = [];
    public required DateTime AsOf { get; init; }

    // Trusted module reports (current-state: Inventory, Reorder; fiscal-year: PurchaseOrders, Receipts)
    public required DashboardSection<InventorySummary> Inventory { get; init; }
    public required DashboardSection<ReorderReport> Reorder { get; init; }
    public required DashboardSection<PurchaseOrderReport> PurchaseOrders { get; init; }
    public required DashboardSection<ReceiptReport> Receipts { get; init; }

    // Dashboard-only legacy KPIs
    public required DashboardSection<DashboardBudget> Budget { get; init; }
    public required DashboardSection<DashboardSubstock> Substock { get; init; }
    public required DashboardSection<DashboardStockCoverage> Coverage { get; init; }
    public required DashboardSection<IReadOnlyList<DashboardEdNedRow>> EdNed { get; init; }
    public required DashboardSection<DashboardAgreements> Agreements { get; init; }
    public required DashboardSection<IReadOnlyList<DashboardMovementMonth>> Movement { get; init; }
    public required DashboardSection<IReadOnlyList<DashboardProcessTimeRow>> ProcessTime { get; init; }
    public required DashboardSection<DashboardItemTrend?> ItemTrend { get; init; }

    public int FailedSectionCount =>
        new bool[] { Inventory.IsAvailable, Reorder.IsAvailable, PurchaseOrders.IsAvailable, Receipts.IsAvailable, Budget.IsAvailable,
                     Substock.IsAvailable, Coverage.IsAvailable, EdNed.IsAvailable, Agreements.IsAvailable, Movement.IsAvailable,
                     ProcessTime.IsAvailable, ItemTrend.IsAvailable }.Count(a => !a);
}

/// <summary>Read-only analytics for the legacy KPIs without a domain owner. Implemented in Infrastructure.</summary>
public interface IDashboardAnalyticsRepository
{
    Task<DashboardBudget> GetBudgetAsync(int fiscalYear, CancellationToken ct = default);
    Task<DashboardSubstock> GetSubstockAsync(CancellationToken ct = default);
    Task<DashboardStockCoverage> GetStockCoverageAsync(CancellationToken ct = default);
    Task<IReadOnlyList<DashboardEdNedRow>> GetLegacyEdNedAsync(CancellationToken ct = default);
    Task<IReadOnlyList<DashboardAgreementRow>> GetActiveAgreementsAsync(DateTime today, CancellationToken ct = default);
    Task<IReadOnlyList<DashboardMovementRow>> GetMovementAsync(int fiscalYear, CancellationToken ct = default);
    Task<IReadOnlyList<DashboardProcessTimeRow>> GetProcessTimeAsync(int fiscalYear, CancellationToken ct = default);
    Task<DashboardItemTrend?> GetItemTrendAsync(string workingCode, int months, CancellationToken ct = default);
}
