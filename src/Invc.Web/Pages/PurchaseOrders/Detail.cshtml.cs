using Invc.Core.PurchaseOrders;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Invc.Web.Pages.PurchaseOrders;

/// <summary>PO detail (port of PODetail.asp): header, MS_PO_C lines, MS_IVO/MS_IVO_C receipts.</summary>
public class DetailModel(IPurchaseOrderRepository purchaseOrders, ILogger<DetailModel> logger) : PageModel
{
    public PurchaseOrderDetail? Order { get; private set; }
    public string? RealPo { get; private set; }
    public bool HasError { get; private set; }
    public bool IsPrint { get; protected set; }
    public DateTime Today { get; } = DateTime.Today;

    public async Task<IActionResult> OnGetAsync(string? realPo, CancellationToken cancellationToken)
    {
        RealPo = realPo?.Trim();
        if (!PurchaseOrderRules.IsValidPoNumber(RealPo))
        {
            return NotFound();
        }

        try
        {
            Order = await purchaseOrders.GetDetailAsync(RealPo!, cancellationToken);
            if (Order is not null)
            {
                // owner rule: item lists A–Z by drug name (document totals are order-independent)
                Order = Order with { Lines = Invc.Core.Inventory.DrugNameOrder.Sort(Order.Lines, l => l.DrugName, l => l.WorkingCode) };
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Purchase order detail failed ({RealPo})", RealPo);
            HasError = true;
            return Page();
        }

        return Order is null ? NotFound() : Page();
    }
}
