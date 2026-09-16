using Invc.Core.PurchaseOrders;

namespace Invc.Web.Pages.PurchaseOrders;

/// <summary>Print-friendly PO detail — identical loading to <see cref="DetailModel"/>, layout-less page.</summary>
public class PrintModel : DetailModel
{
    public PrintModel(IPurchaseOrderRepository purchaseOrders, ILogger<DetailModel> logger) : base(purchaseOrders, logger)
    {
        IsPrint = true;
    }
}
