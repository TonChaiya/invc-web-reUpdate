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

/// <summary>
/// Item-type dimension of the same CARD rows (2026-09-18): CARD.WORKING_CODE → INV_MD.ED_NED → TBLED_NED (EDCODE/EDNAME),
/// resolved with deterministic TOP 1 lookups so CARD rows are never multiplied. Orthogonal to <see cref="MovementCategory"/>
/// (RO/RS/SS/SO = transaction direction/source). Null/blank/unmapped codes stay as an explicit "ไม่ระบุประเภท" bucket.
/// </summary>
public sealed record DashboardMovementItemTypeRow(string MonthKey, string? Status, string? NumberPrefix, string? ItemTypeCode, string? ItemTypeName, int Count, decimal Value, decimal Quantity);

/// <summary>Per-month value of one item type by direction (presentation aggregation only).</summary>
public sealed record DashboardMovementTypeSummary(string Code, string Name, decimal ReceiveValue, decimal IssueValue, decimal OtherValue, int Count)
{
    public const string UnknownCode = "?";
    public const string UnknownName = "ไม่ระบุประเภท";
    public bool IsUnknown => Code == UnknownCode;
    public bool HasValue => ReceiveValue != 0m || IssueValue != 0m || OtherValue != 0m;
}

public sealed record DashboardMovementMonth(string MonthKey, IReadOnlyList<DashboardMovementCategory> Categories)
{
    /// <summary>Item-type breakdown (codes 1–5 + unknown), EDCODE order; empty when the type query was not supplied.</summary>
    public IReadOnlyList<DashboardMovementTypeSummary> ItemTypes { get; init; } = [];

    public DashboardMovementTypeSummary? Type(string code) => ItemTypes.FirstOrDefault(t => t.Code == code);

    /// <summary>ยา รวม = ED (1) + NED (2): a subtotal for display, never added to the grand total.</summary>
    public decimal DrugReceiveValue => (Type("1")?.ReceiveValue ?? 0m) + (Type("2")?.ReceiveValue ?? 0m);
    public decimal DrugIssueValue => (Type("1")?.IssueValue ?? 0m) + (Type("2")?.IssueValue ?? 0m);
    public decimal DrugOtherValue => (Type("1")?.OtherValue ?? 0m) + (Type("2")?.OtherValue ?? 0m);

    /// <summary>Parity invariants: the type dimension must re-add to the D9 totals of this month.</summary>
    public decimal TypeReceiveTotal => ItemTypes.Sum(t => t.ReceiveValue);
    public decimal TypeIssueTotal => ItemTypes.Sum(t => t.IssueValue);
    public decimal TypeOtherTotal => ItemTypes.Sum(t => t.OtherValue);
    public bool TypesMatchTotals => ItemTypes.Count > 0 && TypeReceiveTotal == ReceiveValue && TypeIssueTotal == IssueValue && TypeOtherTotal == OtherValue;

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

    /// <summary>
    /// D9 months (unchanged) plus the item-type breakdown of each month from the typed rows. Direction of a typed row uses the
    /// same <see cref="MovementCategory.FromRaw"/> rule as the totals, so per-month type sums equal ReceiveValue / IssueValue / OtherValue.
    /// </summary>
    public static IReadOnlyList<DashboardMovementMonth> Build(IEnumerable<DashboardMovementRow> rows, IEnumerable<DashboardMovementItemTypeRow> typedRows)
    {
        var byMonth = typedRows.GroupBy(r => r.MonthKey).ToDictionary(g => g.Key, g => g.ToList());
        return Build(rows).Select(m => m with { ItemTypes = byMonth.TryGetValue(m.MonthKey, out var t) ? Summarize(t) : [] }).ToList();
    }

    internal static IReadOnlyList<DashboardMovementTypeSummary> Summarize(IEnumerable<DashboardMovementItemTypeRow> rows)
        => rows
            .GroupBy(r => string.IsNullOrWhiteSpace(r.ItemTypeCode) ? DashboardMovementTypeSummary.UnknownCode : r.ItemTypeCode!.Trim())
            .Select(g =>
            {
                var code = g.Key;
                var name = code == DashboardMovementTypeSummary.UnknownCode ? DashboardMovementTypeSummary.UnknownName
                         : g.Select(r => r.ItemTypeName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? $"รหัส {code} (ไม่พบใน TBLED_NED)";
                decimal Sum(MovementDirection d) => g.Where(r => MovementCategory.FromRaw(r.Status, r.NumberPrefix).Direction == d).Sum(r => r.Value);
                return new DashboardMovementTypeSummary(code, name, Sum(MovementDirection.Receive), Sum(MovementDirection.Issue), Sum(MovementDirection.Other), g.Sum(r => r.Count));
            })
            .OrderBy(t => t.IsUnknown ? 1 : 0).ThenBy(t => t.Code, StringComparer.Ordinal)
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

    /// <summary>Same CARD window as <see cref="GetMovementAsync"/>, additionally grouped by the item's ED_NED type (TBLED_NED).</summary>
    Task<IReadOnlyList<DashboardMovementItemTypeRow>> GetMovementByItemTypeAsync(int fiscalYear, CancellationToken ct = default);
    Task<IReadOnlyList<DashboardProcessTimeRow>> GetProcessTimeAsync(int fiscalYear, CancellationToken ct = default);
    Task<DashboardItemTrend?> GetItemTrendAsync(string workingCode, int months, CancellationToken ct = default);
}
