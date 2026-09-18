using Invc.Core.PurchaseOrders;

namespace Invc.Web.Pages.PurchaseOrders;

/// <summary>
/// Presentation-only document progression for a purchase order, built strictly from existing MS_PO dates —
/// nothing is inferred: a step is "done" only when its source date is present.
/// สั่งซื้อ = PO_DATE · รับของ = FirstReceiveDate (MS_IVO) · ตั้งหนี้ = BILLOUT_ACC · การเงิน = BILLOUT_FIN · ปิดบัญชี = BILLEND_ACC.
/// </summary>
public static class PoTimeline
{
    public sealed record Step(string Label, DateTime? Date)
    {
        public bool Done => Date is not null;
    }

    public static IReadOnlyList<Step> Steps(PurchaseOrderSummary h) =>
    [
        new("สั่งซื้อ", h.PoDate),
        new("รับของ", h.FirstReceiveDate),
        new("ตั้งหนี้", h.BillOutAcc),
        new("การเงิน", h.BillOutFin),
        new("ปิดบัญชี", h.BillEndAcc),
    ];

    /// <summary>Short phrase for the list/summary: the latest completed step, or "รอรับของ" when nothing has happened after issue.</summary>
    public static string Phrase(PurchaseOrderSummary h)
    {
        var steps = Steps(h);
        var last = steps.Skip(1).LastOrDefault(s => s.Done);
        return last is null ? "รอรับของ" : $"{last.Label} {last.Date!.Value:d MMM yy}";
    }
}
