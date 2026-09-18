namespace Invc.Core.Inventory;

/// <summary>
/// ประเภทเวชภัณฑ์ (INVC "สร้างรหัสยา" dropdown): INV_MD.ED_NED → dbo.TBLED_NED (EDCODE, EDNAME, EDMAP).
/// Live values (2026-09-18): 1 ยาในบัญชียาหลักแห่งชาติ, 2 ยานอกบัญชียาหลักแห่งชาติ, 3 วัสดุการแพทย์, 4 วัสดุเภสัชกรรม, 5 ยาตัวอย่างเพื่อทดลองใช้.
/// Three different dimensions must never be confused: ED_NED = ประเภทเวชภัณฑ์ (this), GROUP_CODE → dbo.[GROUP] = กลุ่มยา
/// (pharmacological group — not implemented yet), LOCATION = ที่เก็บ. SUPPLY_TYPE is deprecated in INVC; ED_NED replaces it.
/// </summary>
public sealed record ItemType(string Code, string Name, string? ShortName)
{
    public string DisplayLabel => $"{Code} — {Name}";

    /// <summary>EDCODE is nchar(1); a query value is accepted only when it names one of the known types.</summary>
    public static string? NormalizeCode(string? value, IReadOnlyList<ItemType> known)
    {
        var v = value?.Trim();
        return v is { Length: 1 } && known.Any(t => t.Code == v) ? v : null;
    }
}
