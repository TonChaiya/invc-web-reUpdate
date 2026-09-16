using Invc.Core.Common;

namespace Invc.Core.PurchaseOrders;

/// <summary>
/// Process buckets of the legacy purchasing dashboard (Dashboard.asp / table_ipiss.asp), defined by
/// sets of <c>MS_PO.STATUS</c> codes. This is the single implementation; SQL never encodes these sets.
/// </summary>
public enum PurchaseOrderBucket
{
    /// <summary>มูลค่าใบสั่งซื้อ — STATUS NOT IN ('0','C'). Legacy default view.</summary>
    Issued,
    /// <summary>ตรวจรับแล้ว — STATUS IN ('2','3','4','5','6','7','8','9','D').</summary>
    Received,
    /// <summary>ส่งตั้งหนี้ — STATUS IN ('4','5','6','7','8','9','D').</summary>
    Accounting,
    /// <summary>ส่งเอกสารการเงิน — STATUS IN ('4','5','7','8','9').</summary>
    Finance,
    /// <summary>เสร็จสิ้น — STATUS IN ('5','9').</summary>
    Closed,
    /// <summary>Every PO of the fiscal year including cancelled ('0') and pending-cancel ('C'). UX addition.</summary>
    All,
}

public static class PurchaseOrderRules
{
    private static readonly HashSet<string> Cancelled = ["0", "C"];
    private static readonly HashSet<string> ReceivedSet = ["2", "3", "4", "5", "6", "7", "8", "9", "D"];
    private static readonly HashSet<string> AccountingSet = ["4", "5", "6", "7", "8", "9", "D"];
    private static readonly HashSet<string> FinanceSet = ["4", "5", "7", "8", "9"];
    private static readonly HashSet<string> ClosedSet = ["5", "9"];

    /// <summary>Verbatim legacy status sets — exposed for documentation and tests.</summary>
    public static IReadOnlySet<string> ReceivedStatuses => ReceivedSet;
    public static IReadOnlySet<string> AccountingStatuses => AccountingSet;
    public static IReadOnlySet<string> FinanceStatuses => FinanceSet;
    public static IReadOnlySet<string> ClosedStatuses => ClosedSet;
    public static IReadOnlySet<string> CancelledStatuses => Cancelled;

    public static bool IsInBucket(string? status, PurchaseOrderBucket bucket)
    {
        var s = (status ?? string.Empty).Trim();
        if (bucket == PurchaseOrderBucket.All)
        {
            return true;
        }

        // Legacy SQL `STATUS NOT IN ('0','C')` evaluates to UNKNOWN for a NULL status, so such a PO was not
        // counted as issued either; it is visible only under "all" (with an empty status).
        if (s.Length == 0)
        {
            return false;
        }

        return bucket switch
        {
            PurchaseOrderBucket.Issued => !Cancelled.Contains(s),
            PurchaseOrderBucket.Received => ReceivedSet.Contains(s),
            PurchaseOrderBucket.Accounting => AccountingSet.Contains(s),
            PurchaseOrderBucket.Finance => FinanceSet.Contains(s),
            PurchaseOrderBucket.Closed => ClosedSet.Contains(s),
            _ => false,
        };
    }

    /// <summary>Query-string value → bucket; blank/unknown → Issued (legacy "PO" view).</summary>
    public static PurchaseOrderBucket ParseBucket(string? value)
        => value?.Trim().ToLowerInvariant() switch
        {
            "received" => PurchaseOrderBucket.Received,
            "accounting" => PurchaseOrderBucket.Accounting,
            "finance" => PurchaseOrderBucket.Finance,
            "closed" => PurchaseOrderBucket.Closed,
            "all" => PurchaseOrderBucket.All,
            _ => PurchaseOrderBucket.Issued,
        };

    public static string ToQueryValue(this PurchaseOrderBucket bucket) => bucket.ToString().ToLowerInvariant();

    public static string ThaiLabel(this PurchaseOrderBucket bucket)
        => bucket switch
        {
            PurchaseOrderBucket.Issued => "ออกใบสั่งซื้อแล้ว",
            PurchaseOrderBucket.Received => "ตรวจรับแล้ว",
            PurchaseOrderBucket.Accounting => "ส่งตั้งหนี้",
            PurchaseOrderBucket.Finance => "ส่งเอกสารการเงิน",
            PurchaseOrderBucket.Closed => "เสร็จสิ้น",
            _ => "ทั้งหมด",
        };

    /// <summary>
    /// Legacy fiscal-year rule: a PO belongs to the fiscal year encoded in the first two characters of PO_NO
    /// (6900001 → 2569). PO_DATE is not used. Returns null when the prefix is not two digits.
    /// </summary>
    public static int? FiscalYearFromPoNumber(string? poNo)
    {
        if (poNo is null || poNo.Length < 2 || !char.IsDigit(poNo[0]) || !char.IsDigit(poNo[1]))
        {
            return null;
        }

        return 2500 + int.Parse(poNo[..2]);
    }

    /// <summary>Buddhist fiscal year → two-character PO prefix via the shared utility (2569 → "69").</summary>
    public static string PoPrefix(int buddhistFiscalYear) => ThaiFiscalYear.ToPoNumberPrefix(buddhistFiscalYear);

    /// <summary>Accepts a user-supplied fiscal year (2500–2599 covers the two-digit PO prefix scheme).</summary>
    public static int? TryParseFiscalYear(string? value)
        => int.TryParse(value?.Trim(), out var y) && y is >= 2500 and <= 2599 ? y : null;

    /// <summary>
    /// Legacy table_ipiss.asp PROCESSDAY: DATEDIFF(DAY, FIRST_RCVDATE, GETDATE()) — the legacy CASE on BILL_PAY_FIN
    /// had identical branches, so the count keeps running after payment. Preserved as-is; null when no receive date.
    /// </summary>
    public static int? ProcessDays(DateTime? firstReceiveDate, DateTime today)
        => firstReceiveDate is { } d ? (today.Date - d.Date).Days : null;

    /// <summary>Legacy PODetail.asp: packs = QTY_ORDER / PACK_RATIO1; null instead of dividing by zero.</summary>
    public static decimal? Packs(decimal? qty, decimal? packRatio)
        => packRatio is > 0m && qty is { } q ? q / packRatio.Value : null;

    /// <summary>REAL_PO / PO_NO identifiers: 1–10 characters, letters, digits, '-', '_', '.', '/' (nvarchar(10) in INV).</summary>
    public const int PoNumberMaxLength = 10;

    public static bool IsValidPoNumber(string? value)
        => !string.IsNullOrEmpty(value)
           && value.Length <= PoNumberMaxLength
           && value.All(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.' or '/');
}
