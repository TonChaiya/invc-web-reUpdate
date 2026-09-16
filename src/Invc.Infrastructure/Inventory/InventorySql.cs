namespace Invc.Infrastructure.Inventory;

/// <summary>
/// SQL for the inventory status report. Derived from INV_Status.asp
/// (docs/legacy-query-map.md). Differences from the legacy page are deliberate and documented:
///  * explicit column list instead of <c>SELECT INV_MD.*</c> (INV_MD has an nvarchar(max) column);
///  * the per-row VCAR/BORROW lookup (N+1) is folded into one LEFT JOIN;
///  * ordering uses <c>LEN(WORKING_CODE), WORKING_CODE</c>, which yields the same order as the legacy
///    <c>CAST(WORKING_CODE AS INT)</c> for all-digit codes but cannot fail on non-numeric codes
///    (TRY_CAST is unavailable: the database runs at compatibility level 100);
///  * the HIS name lookup (<c>Med_inv</c>) is omitted — that table does not exist in INV.
/// </summary>
internal static class InventorySql
{
    private const string BorrowableSubquery = """
        LEFT JOIN (
            SELECT v.WORKING_CODE,
                   SUM(v.BORROW_QTY - ISNULL(b.BORROWED, 0)) AS BORROWABLE
            FROM dbo.VCAR v
            LEFT JOIN (
                SELECT VCAR_CODE, WORKING_CODE, SUM(BORROW_QTY) AS BORROWED
                FROM dbo.BORROW
                GROUP BY VCAR_CODE, WORKING_CODE
            ) b ON b.VCAR_CODE = v.RECORD_NUMBER AND b.WORKING_CODE = v.WORKING_CODE
            WHERE v.BORROW_QTY <> 0 AND v.CLOSE_STATUS <> 'C'
            GROUP BY v.WORKING_CODE
        ) bo ON bo.WORKING_CODE = m.WORKING_CODE
        """;

    private const string SelectList = """
        SELECT m.WORKING_CODE           AS WorkingCode,
               m.DRUG_NAME              AS DrugName,
               m.HOSP_CODE              AS HospCode,
               m.VEN                    AS Ven,
               ISNULL(m.QTY_ON_HAND, 0) AS QtyOnHand,
               m.SALE_UNIT              AS SaleUnit,
               m.LOCATION               AS Location,
               m.RATE_PER_MONTH         AS RatePerMonth,
               ISNULL(bo.BORROWABLE, 0) AS BorrowableQty
        FROM dbo.INV_MD m
        """;

    private const string OrderBy = """
        ORDER BY LEN(m.WORKING_CODE), m.WORKING_CODE, m.DRUG_NAME
        """;

    /// <summary>All active items (legacy: SELECT * FROM INV_MD WHERE [NOUSE] IS NULL).</summary>
    public const string StatusAll = $"""
        {SelectList}
        {BorrowableSubquery}
        WHERE m.NOUSE IS NULL
        {OrderBy}
        """;

    /// <summary>Keyword search over the same four columns the legacy page used.</summary>
    public const string StatusSearch = $"""
        {SelectList}
        {BorrowableSubquery}
        WHERE m.NOUSE IS NULL
          AND (   m.DRUG_NAME    LIKE @Pattern
               OR m.COMPOSITION  LIKE @Pattern
               OR m.HOSP_CODE    LIKE @Pattern
               OR m.WORKING_CODE LIKE @Pattern)
        {OrderBy}
        """;

    /// <summary>Headline totals over active items.</summary>
    public const string Summary = """
        SELECT COUNT(*)                       AS ActiveItemCount,
               ISNULL(SUM(m.QTY_ON_HAND), 0)  AS TotalQtyOnHand,
               ISNULL(SUM(m.TOTAL_VALUE), 0)  AS TotalValue
        FROM dbo.INV_MD m
        WHERE m.NOUSE IS NULL
        """;
}
