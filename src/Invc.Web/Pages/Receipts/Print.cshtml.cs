using Invc.Core.Receipts;

namespace Invc.Web.Pages.Receipts;

/// <summary>Print-friendly receipt detail — identical loading to <see cref="DetailModel"/>, layout-less page.</summary>
public class PrintModel : DetailModel
{
    public PrintModel(INonPoReceiptRepository receipts, ILogger<DetailModel> logger) : base(receipts, logger)
    {
        IsPrint = true;
    }
}
