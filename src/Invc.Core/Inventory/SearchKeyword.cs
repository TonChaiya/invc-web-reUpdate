namespace Invc.Core.Inventory;

/// <summary>
/// Normalises the inventory search keyword: trims whitespace, collapses an empty value to null,
/// and bounds the length. The legacy page accepted any length; DRUG_NAME is nvarchar(50),
/// COMPOSITION nvarchar(50), HOSP_CODE nvarchar(10), WORKING_CODE nvarchar(7), so anything longer
/// than 50 characters can never match and is cut off.
/// </summary>
public static class SearchKeyword
{
    public const int MaxLength = 50;

    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var trimmed = raw.Trim();
        return trimmed.Length > MaxLength ? trimmed[..MaxLength] : trimmed;
    }
}
