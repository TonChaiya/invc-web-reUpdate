using System.Globalization;

namespace Invc.Core.Inventory;

/// <summary>
/// Business rules ported verbatim from the legacy Classic ASP pages
/// (see docs/inventory-business-rules.md). These must produce the same
/// results as the legacy pages; do not "improve" them without a parity decision.
/// </summary>
public static class InventoryRules
{
    /// <summary>
    /// INV_Status.asp: months of stock left = ROUND(QTY_ON_HAND / RATE_PER_MONTH, 2);
    /// "N/A" when the rate is NULL/zero; values between 0 and 1 are shown with a
    /// leading "0" (legacy VBScript quirk, e.g. "0.5" → "0.5", ".5" never appears).
    /// </summary>
    public static string MonthsOfStock(decimal qtyOnHand, decimal? ratePerMonth)
    {
        if (ratePerMonth is null || ratePerMonth == 0m)
        {
            return "N/A";
        }

        var months = Math.Round(qtyOnHand / ratePerMonth.Value, 2, MidpointRounding.ToEven);
        return months.ToString("0.##", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// INV_Report_Purchase.asp: reorder point falls back to MIN_LEVEL when REORDER_QTY is 0/NULL.
    /// </summary>
    public static decimal EffectiveReorderPoint(decimal? reorderQty, decimal? minLevel)
        => (reorderQty ?? 0m) == 0m ? (minLevel ?? 0m) : reorderQty!.Value;

    /// <summary>
    /// INV_Report_Purchase.asp status buckets:
    /// red    = QTY_ON_HAND &lt; MIN_LEVEL,
    /// yellow = MIN_LEVEL ≤ QTY_ON_HAND &lt; reorder point,
    /// green  = otherwise.
    /// </summary>
    public static ReorderStatus ClassifyReorder(decimal? qtyOnHand, decimal? minLevel, decimal? reorderQty)
    {
        var stock = qtyOnHand ?? 0m;
        var min = minLevel ?? 0m;
        var rop = EffectiveReorderPoint(reorderQty, minLevel);

        if (stock < min)
        {
            return ReorderStatus.Red;
        }

        if (stock >= min && stock < rop)
        {
            return ReorderStatus.Yellow;
        }

        return ReorderStatus.Green;
    }

    /// <summary>
    /// INV_Report_Purchase.asp: suggested order quantity = CEILING(MAX_LEVEL − QTY_ON_HAND).
    /// The legacy page shows negative values when MAX_LEVEL is 0; callers decide how to present that.
    /// </summary>
    public static decimal SuggestedOrderQty(decimal? maxLevel, decimal? qtyOnHand)
        => Math.Ceiling((maxLevel ?? 0m) - (qtyOnHand ?? 0m));

    /// <summary>
    /// chkstock.asp lot table "คงเหลือ": QTY_ON_HAND / PACK_RATIO expressed in packs.
    /// Returns null when PACK_RATIO is 0 or negative instead of dividing by zero.
    /// </summary>
    public static decimal? PacksOnHand(decimal qtyOnHand, decimal packRatio)
        => packRatio <= 0m ? null : qtyOnHand / packRatio;

    /// <summary>
    /// Per-lot pack expression for the quick lot view: "2 × 100", "2 × 100 + 50", "40" (less than one pack).
    /// Each lot keeps its own PACK_RATIO — ratios are never merged across lots. Returns null when the ratio is
    /// 1 or invalid (≤ 0) or the quantity is not positive, so the caller shows only the total quantity.
    /// </summary>
    /// <summary>
    /// Owner-defined expiry attention levels for the lot views (2026-09-18): expired → <see cref="ExpiryStatus.Expired"/>;
    /// expiring within 1 month → <see cref="ExpiryStatus.Within1Month"/>; within 3 months → <see cref="ExpiryStatus.Within3Months"/>;
    /// otherwise <see cref="ExpiryStatus.Ok"/>. No date → Unknown. Compared by calendar date (time ignored).
    /// </summary>
    public static ExpiryStatus ClassifyExpiry(DateTime? expiredDate, DateTime today)
    {
        if (expiredDate is null)
        {
            return ExpiryStatus.Unknown;
        }

        var exp = expiredDate.Value.Date;
        var day = today.Date;
        if (exp < day) return ExpiryStatus.Expired;
        if (exp <= day.AddMonths(1)) return ExpiryStatus.Within1Month;
        if (exp <= day.AddMonths(3)) return ExpiryStatus.Within3Months;
        return ExpiryStatus.Ok;
    }

    public static string? PackExpression(decimal qtyOnHand, decimal packRatio)
    {
        if (packRatio <= 1m || qtyOnHand <= 0m)
        {
            return null;
        }

        var packs = Math.Floor(qtyOnHand / packRatio);
        var remainder = qtyOnHand - packs * packRatio;
        var ratio = packRatio.ToString("#,##0.##", CultureInfo.InvariantCulture);
        var rem = remainder.ToString("#,##0.##", CultureInfo.InvariantCulture);
        if (packs == 0m)
        {
            return rem;
        }

        var head = $"{packs.ToString("#,##0", CultureInfo.InvariantCulture)} × {ratio}";
        return remainder == 0m ? head : $"{head} + {rem}";
    }
}

/// <summary>Reorder status bucket used by INV_Report_Purchase.asp (red/yellow/green).</summary>
public enum ExpiryStatus
{
    Unknown,
    Ok,
    Within3Months,
    Within1Month,
    Expired,
}

public enum ReorderStatus
{
    Red,
    Yellow,
    Green,
}
