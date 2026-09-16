using Invc.Core.Receipts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Invc.Web.Pages.Receipts;

/// <summary>Non-PO receipt detail: OTH_IVO header + OTH_IVOC lines keyed by RECEIVE_NO. Two statements per request.</summary>
public class DetailModel(INonPoReceiptRepository receipts, ILogger<DetailModel> logger) : PageModel
{
    public NonPoReceiptDetail? Receipt { get; private set; }
    public string? ReceiveNo { get; private set; }
    public bool HasError { get; private set; }
    public bool IsPrint { get; protected set; }

    public async Task<IActionResult> OnGetAsync(string? receiveNo, CancellationToken cancellationToken)
    {
        ReceiveNo = receiveNo?.Trim();
        if (!ReceiptRules.IsValidReceiptNo(ReceiveNo))
        {
            return NotFound();
        }

        try
        {
            Receipt = await receipts.GetDetailAsync(ReceiveNo!, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Non-PO receipt detail failed ({ReceiveNo})", ReceiveNo);
            HasError = true;
            return Page();
        }

        return Receipt is null ? NotFound() : Page();
    }
}
