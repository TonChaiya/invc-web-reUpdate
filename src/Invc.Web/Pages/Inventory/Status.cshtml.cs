using Invc.Core.Inventory;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Invc.Web.Pages.Inventory;

/// <summary>Port of the legacy INV_Status.asp (menu "สถานะคงคลัง").</summary>
public class StatusModel(IInventoryRepository inventory, ILogger<StatusModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Keyword { get; set; }

    public IReadOnlyList<InventoryItem> Items { get; private set; } = [];
    public string? Error { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        if (Keyword is { Length: > 60 })
        {
            Keyword = Keyword[..60];
        }

        try
        {
            Items = await inventory.GetStatusAsync(Keyword, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Inventory status query failed (keyword: {Keyword})", Keyword);
            Error = "ไม่สามารถอ่านข้อมูลคงคลังได้ กรุณาตรวจสอบหน้า สถานะระบบ";
        }
    }
}
