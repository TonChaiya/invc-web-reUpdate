using Invc.Core.Common;

namespace Invc.Core.Receipts;

/// <summary>
/// Rules for non-PO receipts (dbo.OTH_IVO / OTH_IVOC). Evidence: docs/phase5-receipts-data-map.md.
/// Deliberately independent from the purchase-order models (Phase 4) — the two flows share no tables.
/// </summary>
public static class ReceiptRules
{
    /// <summary>OTH_IVO.RECEIVE_NO is nvarchar(10); production values are 'O' + FY prefix + 5 digits.</summary>
    public const int ReceiveNoMaxLength = 10;

    public static bool IsValidReceiptNo(string? value)
        => !string.IsNullOrEmpty(value)
           && value.Length <= ReceiveNoMaxLength
           && value.All(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_');

    /// <summary>Fiscal year of a receipt = Thai fiscal year of OTH_IVO.DATE_RECEIVE (1 Oct – 30 Sep), shared rule.</summary>
    public static int? FiscalYearOf(DateTime? dateReceive)
        => dateReceive is { } d ? ThaiFiscalYear.FromDate(d) : null;

    public static int? TryParseFiscalYear(string? value)
        => int.TryParse(value?.Trim(), out var y) && y is >= 2500 and <= 2599 ? y : null;

    /// <summary>RCV_TYPE codes are two alphanumeric characters; anything else (including "all") means no type filter.</summary>
    public static string? NormalizeTypeCode(string? value)
    {
        var t = value?.Trim();
        return t is { Length: 2 } && t.All(char.IsLetterOrDigit) ? t : null;
    }

    /// <summary>Packs = QTY_ORDER / PACK_RATIO (verified by the header value identity); null when the ratio is 0/NULL.</summary>
    public static decimal? Packs(decimal? qty, decimal? packRatio)
        => packRatio is > 0m && qty is { } q ? q / packRatio.Value : null;

    /// <summary>
    /// Line value = UNIT_VALUE × QTY_ORDER / PACK_RATIO. UNIT_VALUE is the price per pack — proven because
    /// SUM of this expression equals OTH_IVO.TOTAL_VALUE for every header in production (110/110).
    /// </summary>
    public static decimal? LineValue(decimal? unitValue, decimal? qty, decimal? packRatio)
        => unitValue is { } u && Packs(qty, packRatio) is { } packs ? u * packs : null;   // unrounded; pages format to 2 dp
}

/// <summary>One OTH_IVO header for the list page (with line aggregates from OTH_IVOC).</summary>
public sealed record NonPoReceiptSummary
{
    /// <summary>OTH_IVO.RECEIVE_NO — the receipt business key (unique in production, lines join on it).</summary>
    public required string ReceiveNo { get; init; }

    /// <summary>OTH_IVO.INVOICE_NO — the source document/reference number (not unique).</summary>
    public string? InvoiceNo { get; init; }

    public DateTime? InvoiceDate { get; init; }

    /// <summary>OTH_IVO.DATE_RECEIVE — authoritative receipt date and fiscal-year driver.</summary>
    public DateTime? DateReceive { get; init; }

    /// <summary>OTH_IVO.RCV_TYPE (nchar 2, trimmed).</summary>
    public string? TypeCode { get; init; }

    /// <summary>RCV_TYPE.RCV_NAME — null when the lookup row is missing.</summary>
    public string? TypeName { get; init; }

    /// <summary>OTH_IVO.DPT_CODE — the source (resolves in COMPANY.COMPANY_CODE, e.g. CUP parent, another unit, 'OPEN' = opening stock).</summary>
    public string? SourceCode { get; init; }
    public string? SourceName { get; init; }

    /// <summary>OTH_IVO.TOTAL_ITEM — header line count as recorded by Access.</summary>
    public decimal? TotalItem { get; init; }

    /// <summary>OTH_IVO.TOTAL_VALUE — header value (0 for valueless types such as borrow/donation).</summary>
    public decimal? TotalValue { get; init; }

    public string? Note { get; init; }

    /// <summary>COUNT(*) of OTH_IVOC lines with the same RECEIVE_NO.</summary>
    public int LineCount { get; init; }

    /// <summary>SUM(OTH_IVOC.QTY_ORDER) in sale units.</summary>
    public decimal LineQtySum { get; init; }

    public int? FiscalYear => ReceiptRules.FiscalYearOf(DateReceive);
    public string TypeDisplayName => string.IsNullOrWhiteSpace(TypeName) ? "ไม่พบชื่อประเภท" : TypeName!;
    public string SourceDisplayName => string.IsNullOrWhiteSpace(SourceName) ? (SourceCode ?? "–") : SourceName!;
}

/// <summary>Per-type aggregate for the breakdown (derived from the loaded headers).</summary>
public sealed record ReceiptTypeSummary(string Code, string? Name, int HeaderCount, int LineCount, decimal TotalValue)
{
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "ไม่พบชื่อประเภท" : Name!;
}

/// <summary>RCV_TYPE lookup row for the filter control.</summary>
public sealed record ReceiptTypeOption(string Code, string? Name)
{
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "ไม่พบชื่อประเภท" : Name!;
}

/// <summary>Headers of one fiscal year loaded once; type breakdown, totals and filtered rows all derive from that list.</summary>
public sealed class ReceiptReport
{
    private ReceiptReport(int fiscalYear, IReadOnlyList<NonPoReceiptSummary> headers, string? typeCode, string? keyword)
    {
        FiscalYear = fiscalYear;
        Headers = headers;
        TypeCode = typeCode;
        Keyword = keyword;
        Rows = typeCode is null ? headers : headers.Where(h => string.Equals(h.TypeCode, typeCode, StringComparison.OrdinalIgnoreCase)).ToList();
        TypeBreakdown = headers
            .GroupBy(h => h.TypeCode ?? string.Empty)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new ReceiptTypeSummary(g.Key, g.Select(h => h.TypeName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)),
                                                g.Count(), g.Sum(h => h.LineCount), g.Sum(h => h.TotalValue ?? 0m)))
            .ToList();
    }

    public int FiscalYear { get; }
    public string? TypeCode { get; }
    public string? Keyword { get; }
    public IReadOnlyList<NonPoReceiptSummary> Headers { get; }
    public IReadOnlyList<NonPoReceiptSummary> Rows { get; }
    public IReadOnlyList<ReceiptTypeSummary> TypeBreakdown { get; }

    public int HeaderCount => Headers.Count;
    public int LineCount => Headers.Sum(h => h.LineCount);
    public decimal TotalValue => Headers.Sum(h => h.TotalValue ?? 0m);
    public int TypeCount => TypeBreakdown.Count;

    public static ReceiptReport Build(int fiscalYear, IReadOnlyList<NonPoReceiptSummary> headers, string? typeCode, string? keyword)
        => new(fiscalYear, headers, typeCode, keyword);
}

/// <summary>OTH_IVOC line.</summary>
public sealed record NonPoReceiptLine
{
    public required string WorkingCode { get; init; }
    /// <summary>INV_MD.DRUG_NAME — null when the item master row is missing.</summary>
    public string? DrugName { get; init; }
    public decimal? QtyOrder { get; init; }
    public decimal? PackRatio { get; init; }
    /// <summary>OTH_IVOC.UNIT_VALUE — price per pack.</summary>
    public decimal? UnitValue { get; init; }
    public DateTime? ExpiredDate { get; init; }
    public string? LotNo { get; init; }
    public string? Location { get; init; }
    public string? ManufacCode { get; init; }
    public string? VendorCode { get; init; }

    public decimal? Packs => ReceiptRules.Packs(QtyOrder, PackRatio);
    public decimal? LineValue => ReceiptRules.LineValue(UnitValue, QtyOrder, PackRatio);
    public string DisplayName => string.IsNullOrWhiteSpace(DrugName) ? $"{WorkingCode} (ไม่พบชื่อรายการ)" : DrugName!;
}

/// <summary>Header vs. lines: TOTAL_ITEM vs line count and TOTAL_VALUE vs Σ line value (both identities hold in production).</summary>
public sealed record ReceiptReconciliation(decimal? HeaderItemCount, int LineCount, decimal? HeaderValue, decimal LineValueSum, bool AllLinesValuable)
{
    public bool ItemCountMatches => (HeaderItemCount ?? 0m) == LineCount;
    public decimal ValueDelta => (HeaderValue ?? 0m) - LineValueSum;
    public bool ValueMatches => Math.Abs(ValueDelta) < 0.005m;
    public bool IsConsistent => ItemCountMatches && ValueMatches;
}

public sealed record NonPoReceiptDetail
{
    public required NonPoReceiptSummary Header { get; init; }
    public required IReadOnlyList<NonPoReceiptLine> Lines { get; init; }

    public ReceiptReconciliation Reconciliation
        => new(Header.TotalItem, Lines.Count, Header.TotalValue, Lines.Sum(l => l.LineValue ?? 0m), Lines.All(l => l.LineValue.HasValue));
}

public interface INonPoReceiptRepository
{
    /// <summary>Fiscal years (from DATE_RECEIVE) that contain receipts, descending.</summary>
    Task<IReadOnlyList<int>> GetFiscalYearsWithDataAsync(CancellationToken cancellationToken = default);

    /// <summary>All RCV_TYPE lookup rows, by code.</summary>
    Task<IReadOnlyList<ReceiptTypeOption>> GetTypesAsync(CancellationToken cancellationToken = default);

    /// <summary>Headers whose DATE_RECEIVE falls in the fiscal year (typed date range), with line aggregates; optional keyword.</summary>
    Task<IReadOnlyList<NonPoReceiptSummary>> GetHeadersAsync(int fiscalYear, string? keyword, CancellationToken cancellationToken = default);

    /// <summary>Header + lines for one RECEIVE_NO; null when not found or invalid.</summary>
    Task<NonPoReceiptDetail?> GetDetailAsync(string receiveNo, CancellationToken cancellationToken = default);
}

/// <summary>Default report period (Step 11): current Thai FY if it has receipts, else the latest FY with data.</summary>
public static class ReceiptDefaultPeriod
{
    public static int Choose(IReadOnlyList<int> fiscalYearsWithData, DateTime today, out string? note)
    {
        note = null;
        var current = ThaiFiscalYear.FromDate(today);
        if (fiscalYearsWithData.Contains(current))
        {
            return current;
        }

        if (fiscalYearsWithData.Count > 0)
        {
            var latest = fiscalYearsWithData.Max();
            note = $"ปีงบประมาณปัจจุบัน ({current}) ยังไม่มีรายการรับเข้า — แสดงปีงบประมาณล่าสุดที่มีข้อมูล ({latest})";
            return latest;
        }

        note = "ยังไม่มีรายการรับเข้าในฐานข้อมูล";
        return current;
    }
}
