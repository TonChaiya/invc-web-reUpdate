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

/// <summary>Buddhist "yyyymm" month keys used by every dashboard month list (item trend, processed months).</summary>
public static class DashboardMonthKey
{
    internal static readonly string[] ThaiMonths = ["ม.ค.", "ก.พ.", "มี.ค.", "เม.ย.", "พ.ค.", "มิ.ย.", "ก.ค.", "ส.ค.", "ก.ย.", "ต.ค.", "พ.ย.", "ธ.ค."];

    public static string Display(string monthKey)
        => monthKey.Length == 6 && int.TryParse(monthKey[4..], out var m) && m is >= 1 and <= 12
            ? $"{ThaiMonths[m - 1]} {monthKey[..4]}"
            : monthKey;

    /// <summary>CE year + month (as stored in MNTH_SUM / MBS_RE_M) → Buddhist yyyymm key.</summary>
    public static string FromCe(int ceYear, int month) => $"{ceYear + ThaiFiscalYear.BuddhistEraOffset}{month:00}";

    /// <summary>The 12 Buddhist month keys of a Thai fiscal year (Oct of the previous CE year … Sep).</summary>
    public static IReadOnlyList<string> FiscalYearMonths(int buddhistFiscalYear)
    {
        var ceStart = buddhistFiscalYear - ThaiFiscalYear.BuddhistEraOffset - 1;   // October of FY-1 (CE)
        return Enumerable.Range(0, 12).Select(i => { var d = new DateOnly(ceStart, 10, 1).AddMonths(i); return FromCe(d.Year, d.Month); }).ToList();
    }

    /// <summary>The calendar month immediately before <paramref name="monthKey"/> (Buddhist yyyymm).</summary>
    public static string Previous(string monthKey)
    {
        var y = int.Parse(monthKey[..4]); var m = int.Parse(monthKey[4..]);
        return m == 1 ? $"{y - 1}12" : $"{y}{m - 1:00}";
    }
}

/// <summary>
/// One aggregated MNTH_SUM cell: complete end-of-month snapshot for (CE year, month, historical ED_NED).
/// Loaded for the fiscal year PLUS the immediately preceding calendar month (only as the opening source of the first FY month).
/// </summary>
public sealed record DashboardProcessedSnapshotRow(int Year, int Month, string? EdNed, string? EdNedName, int ItemCount, decimal QtyRemain, decimal TotalValue);

/// <summary>One aggregated MBS_RE_M cell: monthly receive / issue flow for (CE year, month, historical ED_NED). Only items with movement exist here.</summary>
public sealed record DashboardProcessedFlowRow(int Year, int Month, string? EdNed, string? EdNedName, int ItemCount, decimal RcvQuan, decimal RcvValue, decimal SaleQuan, decimal SaleValue);

/// <summary>Opening + receive − issue = ending for one item type inside one processed month (value; quantity kept for validation).</summary>
public sealed record DashboardMonthlyTypeBalance(string Code, string Name, decimal? OpeningValue, decimal ReceiveValue, decimal IssueValue, decimal EndingValue,
                                                 decimal? OpeningQty, decimal ReceiveQty, decimal IssueQty, decimal EndingQty)
{
    public const string UnknownCode = "?";
    public const string UnknownName = "ไม่ระบุประเภท";
    public bool IsUnknown => Code == UnknownCode;
    public bool HasOpening => OpeningValue.HasValue;
    public decimal? CalculatedEnding => OpeningValue is { } o ? o + ReceiveValue - IssueValue : null;
    /// <summary>Actual − calculated; null when there is no opening. Never used to alter values.</summary>
    public decimal? Difference => CalculatedEnding is { } c ? EndingValue - c : null;
    public bool HasAnyValue => OpeningValue is not null and not 0m || ReceiveValue != 0m || IssueValue != 0m || EndingValue != 0m;
}

/// <summary>
/// One INVC-processed month (exists only when MNTH_SUM has it). Opening = previous CALENDAR month's MNTH_SUM ending
/// (null when that month was not processed — never bridged to an older month, never shown as 0).
/// </summary>
public sealed record DashboardProcessedMonth(string MonthKey, string? PreviousMonthKey, decimal? OpeningValue, decimal ReceiveValue, decimal IssueValue, decimal EndingValue,
                                             decimal? OpeningQty, decimal ReceiveQty, decimal IssueQty, decimal EndingQty, int ItemCount, IReadOnlyList<DashboardMonthlyTypeBalance> Types)
{
    public string DisplayMonth => DashboardMonthKey.Display(MonthKey);
    public bool HasOpening => OpeningValue.HasValue;
    public decimal? CalculatedEnding => OpeningValue is { } o ? o + ReceiveValue - IssueValue : null;
    public decimal? Difference => CalculatedEnding is { } c ? EndingValue - c : null;
    public decimal? QtyDifference => OpeningQty is { } o ? EndingQty - (o + ReceiveQty - IssueQty) : null;

    public DashboardMonthlyTypeBalance? Type(string code) => Types.FirstOrDefault(t => t.Code == code);
    /// <summary>ยา รวม = ED (1) + NED (2): display subtotal only, never added to the grand total.</summary>
    public decimal? DrugOpeningValue => Type("1")?.OpeningValue is null && Type("2")?.OpeningValue is null ? null : (Type("1")?.OpeningValue ?? 0m) + (Type("2")?.OpeningValue ?? 0m);
    public decimal DrugReceiveValue => (Type("1")?.ReceiveValue ?? 0m) + (Type("2")?.ReceiveValue ?? 0m);
    public decimal DrugIssueValue => (Type("1")?.IssueValue ?? 0m) + (Type("2")?.IssueValue ?? 0m);
    public decimal DrugEndingValue => (Type("1")?.EndingValue ?? 0m) + (Type("2")?.EndingValue ?? 0m);

    /// <summary>Grand total over actual categories (1..5 + unknown) — must equal the month figures.</summary>
    public decimal? TypeOpeningTotal => Types.Any(t => t.OpeningValue.HasValue) ? Types.Sum(t => t.OpeningValue ?? 0m) : null;
    public decimal TypeReceiveTotal => Types.Sum(t => t.ReceiveValue);
    public decimal TypeIssueTotal => Types.Sum(t => t.IssueValue);
    public decimal TypeEndingTotal => Types.Sum(t => t.EndingValue);
    public bool TypesMatchTotals => TypeReceiveTotal == ReceiveValue && TypeIssueTotal == IssueValue && TypeEndingTotal == EndingValue && (TypeOpeningTotal ?? 0m) == (OpeningValue ?? 0m);
}

public sealed record DashboardProcessedMovement(int FiscalYear, IReadOnlyList<DashboardProcessedMonth> Months)
{
    public DashboardProcessedMonth? Latest => Months.FirstOrDefault();
    public int ProcessedMonthCount => Months.Count;
}

/// <summary>
/// Builds the processed monthly report from MNTH_SUM (snapshots) and MBS_RE_M (flows), verified 2026-09-18 against live INV:
/// previous MNTH_SUM ending + MBS_RE_M receive − MBS_RE_M issue = current MNTH_SUM ending, exactly, per month and per ED_NED.
/// MBS_RE_M.REMAIN_* is deliberately not used (it covers only items with movement). Months come only from MNTH_SUM.
/// </summary>
public static class DashboardProcessedMovementBuilder
{
    public static DashboardProcessedMovement Build(int fiscalYear, IEnumerable<DashboardProcessedSnapshotRow> snapshots, IEnumerable<DashboardProcessedFlowRow> flows)
    {
        var fyMonths = DashboardMonthKey.FiscalYearMonths(fiscalYear);
        var snapByMonth = snapshots.GroupBy(s => DashboardMonthKey.FromCe(s.Year, s.Month)).ToDictionary(g => g.Key, g => g.ToList());
        var flowByMonth = flows.GroupBy(f => DashboardMonthKey.FromCe(f.Year, f.Month)).ToDictionary(g => g.Key, g => g.ToList());

        var months = new List<DashboardProcessedMonth>();
        foreach (var key in fyMonths.Where(snapByMonth.ContainsKey).OrderByDescending(k => k, StringComparer.Ordinal))
        {
            var prevKey = DashboardMonthKey.Previous(key);
            var hasPrev = snapByMonth.TryGetValue(prevKey, out var prevSnap);            // only the immediately preceding calendar month
            var cur = snapByMonth[key];
            var flow = flowByMonth.TryGetValue(key, out var f) ? f : [];

            var codes = cur.Select(s => Code(s.EdNed)).Concat(flow.Select(x => Code(x.EdNed))).Concat(hasPrev ? prevSnap!.Select(s => Code(s.EdNed)) : [])
                           .Distinct().OrderBy(c => c == DashboardMonthlyTypeBalance.UnknownCode ? 1 : 0).ThenBy(c => c, StringComparer.Ordinal).ToList();
            var types = codes.Select(code =>
            {
                var c = cur.Where(s => Code(s.EdNed) == code).ToList();
                var p = hasPrev ? prevSnap!.Where(s => Code(s.EdNed) == code).ToList() : null;
                var fl = flow.Where(x => Code(x.EdNed) == code).ToList();
                var name = code == DashboardMonthlyTypeBalance.UnknownCode ? DashboardMonthlyTypeBalance.UnknownName
                         : c.Select(s => s.EdNedName).Concat(fl.Select(x => x.EdNedName)).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? $"รหัส {code} (ไม่พบใน TBLED_NED)";
                return new DashboardMonthlyTypeBalance(code, name,
                    hasPrev ? p!.Sum(s => s.TotalValue) : null, fl.Sum(x => x.RcvValue), fl.Sum(x => x.SaleValue), c.Sum(s => s.TotalValue),
                    hasPrev ? p!.Sum(s => s.QtyRemain) : null, fl.Sum(x => x.RcvQuan), fl.Sum(x => x.SaleQuan), c.Sum(s => s.QtyRemain));
            }).ToList();

            months.Add(new DashboardProcessedMonth(key, hasPrev ? prevKey : null,
                hasPrev ? prevSnap!.Sum(s => s.TotalValue) : null, flow.Sum(x => x.RcvValue), flow.Sum(x => x.SaleValue), cur.Sum(s => s.TotalValue),
                hasPrev ? prevSnap!.Sum(s => s.QtyRemain) : null, flow.Sum(x => x.RcvQuan), flow.Sum(x => x.SaleQuan), cur.Sum(s => s.QtyRemain),
                cur.Sum(s => s.ItemCount), types));
        }
        return new DashboardProcessedMovement(fiscalYear, months);
    }

    private static string Code(string? edNed) => string.IsNullOrWhiteSpace(edNed) ? DashboardMonthlyTypeBalance.UnknownCode : edNed.Trim();
}

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
        ? $"{DashboardMonthKey.ThaiMonths[m - 1]} {y + ThaiFiscalYear.BuddhistEraOffset}"
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
    /// <summary>INVC-processed monthly report (MNTH_SUM + MBS_RE_M), replaces the former CARD-based monthly movement.</summary>
    public required DashboardSection<DashboardProcessedMovement> Movement { get; init; }
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
    /// <summary>MNTH_SUM aggregated per (YEAR, MONTH, historical ED_NED) for the fiscal year's months PLUS the immediately preceding calendar month.</summary>
    Task<IReadOnlyList<DashboardProcessedSnapshotRow>> GetProcessedSnapshotsAsync(int fiscalYear, CancellationToken ct = default);

    /// <summary>MBS_RE_M aggregated per (YEAR, MONTH, historical ED_NED) for the fiscal year's months.</summary>
    Task<IReadOnlyList<DashboardProcessedFlowRow>> GetProcessedFlowsAsync(int fiscalYear, CancellationToken ct = default);

    Task<IReadOnlyList<DashboardProcessTimeRow>> GetProcessTimeAsync(int fiscalYear, CancellationToken ct = default);
    Task<DashboardItemTrend?> GetItemTrendAsync(string workingCode, int months, CancellationToken ct = default);
}
