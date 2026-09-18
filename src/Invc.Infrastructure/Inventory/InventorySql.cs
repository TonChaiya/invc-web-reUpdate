namespace Invc.Infrastructure.Inventory;

/// <summary>
/// SQL for the inventory module. Derived from INV_Status.asp and chkstock.asp
/// (docs/legacy-query-map.md). Every statement is a single SELECT and passes <c>ReadOnlySql.Ensure</c>.
///
/// Deliberate differences from the legacy pages (see docs/phase2-inventory-parity.md):
///  * explicit column lists instead of <c>SELECT INV_MD.*</c> / <c>MD.*</c> (INV_MD has an nvarchar(max) column);
///  * the per-row VCAR/BORROW lookup (N+1) is folded into one LEFT JOIN, summing all open claims per item
///    (the legacy page read only the first, unordered, VCAR row);
///  * ordering: legacy used <c>CAST(WORKING_CODE AS INT)</c>, which throws on any non-numeric code and is
///    unavailable as TRY_CAST (compatibility level 100). We sort all-digit codes by their zero-padded value
///    (identical order to the numeric cast, including leading zeros) and put any non-numeric code after them;
///  * the HIS name lookup (<c>Med_inv</c>) is omitted — that table does not exist in INV;
///  * lot joins to DRUG_VN / COMPANY use OUTER APPLY (TOP 1) because COMPANY_CODE and the DRUG_VN key are not
///    unique in production (COMPANY_CODE 'MED015' is duplicated); a plain LEFT JOIN would duplicate lot rows.
/// </summary>
internal static class InventorySql
{
    /// <summary>
    /// Sort expression equivalent to the legacy <c>CAST(WORKING_CODE AS INT)</c> for digit-only codes.
    /// Digit-only codes are right-aligned in a 20-character zero-padded field so string order equals numeric order.
    /// </summary>
    private const string SortKey = """
        CASE WHEN m.WORKING_CODE NOT LIKE '%[^0-9]%' THEN 0 ELSE 1 END,
        CASE WHEN m.WORKING_CODE NOT LIKE '%[^0-9]%'
             THEN RIGHT(REPLICATE('0', 20) + m.WORKING_CODE, 20)
             ELSE m.WORKING_CODE END,
        m.DRUG_NAME COLLATE Thai_CI_AS
        """;

    /// <summary>
    /// Legacy INV_Status.asp borrow rule, set-based: per item, SUM over open vendor claims
    /// (VCAR.BORROW_QTY &lt;&gt; 0 AND CLOSE_STATUS &lt;&gt; 'C' — a NULL CLOSE_STATUS is excluded, exactly as the legacy
    /// predicate did) of BORROW_QTY minus the quantity already borrowed against that claim
    /// (BORROW rows matched on VCAR_CODE = VCAR.RECORD_NUMBER AND WORKING_CODE).
    /// <paramref name="vcarSource"/> (aliased <c>v</c>) / <paramref name="borrowSource"/> default to the production tables;
    /// tests pass table-value constructors to exercise the same SQL shape without writing to the database.
    /// </summary>
    internal static string BorrowableJoin(string vcarSource = "dbo.VCAR v", string borrowSource = "dbo.BORROW") => $"""
        LEFT JOIN (
            SELECT v.WORKING_CODE,
                   SUM(v.BORROW_QTY - ISNULL(b.BORROWED, 0)) AS BORROWABLE
            FROM {vcarSource}
            LEFT JOIN (
                SELECT VCAR_CODE, WORKING_CODE, SUM(BORROW_QTY) AS BORROWED
                FROM {borrowSource}
                GROUP BY VCAR_CODE, WORKING_CODE
            ) b ON b.VCAR_CODE = v.RECORD_NUMBER AND b.WORKING_CODE = v.WORKING_CODE
            WHERE v.BORROW_QTY <> 0 AND v.CLOSE_STATUS <> 'C'
            GROUP BY v.WORKING_CODE
        ) bo ON bo.WORKING_CODE = m.WORKING_CODE
        """;

    private static readonly string BorrowableSubquery = BorrowableJoin();

    private const string StatusSelectList = """
        SELECT m.WORKING_CODE           AS WorkingCode,
               m.DRUG_NAME              AS DrugName,
               m.HOSP_CODE              AS HospCode,
               m.VEN                    AS Ven,
               ISNULL(m.QTY_ON_HAND, 0) AS QtyOnHand,
               m.SALE_UNIT              AS SaleUnit,
               m.LOCATION               AS Location,
               m.RATE_PER_MONTH         AS RatePerMonth,
               CAST(ISNULL(bo.BORROWABLE, 0) AS decimal(14, 0)) AS BorrowableQty
        FROM dbo.INV_MD m
        """;

    /// <summary>All active items (legacy: SELECT * FROM INV_MD WHERE [NOUSE] IS NULL).</summary>
    public static readonly string StatusAll = $"""
        {StatusSelectList}
        {BorrowableSubquery}
        WHERE m.NOUSE IS NULL
        ORDER BY {SortKey}
        """;

    /// <summary>Keyword search over the same four columns the legacy page used.</summary>
    public static readonly string StatusSearch = $"""
        {StatusSelectList}
        {BorrowableSubquery}
        WHERE m.NOUSE IS NULL
          AND (   m.DRUG_NAME    LIKE @Pattern
               OR m.COMPOSITION  LIKE @Pattern
               OR m.HOSP_CODE    LIKE @Pattern
               OR m.WORKING_CODE LIKE @Pattern)
        ORDER BY {SortKey}
        """;

    /// <summary>
    /// Quick lot view for the Status page: all INV_MD_C lots of the active (optionally keyword-filtered) items in one
    /// statement — same predicate as <see cref="StatusAll"/>/<see cref="StatusSearch"/>, lot columns as in
    /// <see cref="DetailLots"/> minus vendor/manufacturer (not part of the quick view). Trade name via OUTER APPLY TOP 1
    /// for the same non-unique DRUG_VN key reason.
    /// </summary>
    private const string StatusLotsSelect = """
        SELECT c.WORKING_CODE  AS WorkingCode,
               c.PACK_RATIO    AS PackRatio,
               c.QTY_ON_HAND   AS QtyOnHand,
               c.EXPIRED_DATE  AS ExpiredDate,
               c.LOTNO         AS LotNo,
               c.LOCATION      AS Location,
               c.LOT_VALUE     AS LotValue,
               d.TRADE_NAME    AS TradeName
        FROM dbo.INV_MD_C c
        INNER JOIN dbo.INV_MD m ON m.WORKING_CODE = c.WORKING_CODE
        OUTER APPLY (SELECT TOP 1 x.TRADE_NAME FROM dbo.DRUG_VN x
                     WHERE x.WORKING_CODE = c.WORKING_CODE AND x.PACK_RATIO = c.PACK_RATIO
                       AND x.VENDOR_CODE = c.VENDOR_CODE AND x.MANUFAC_CODE = c.MANUFAC_CODE
                     ORDER BY x.RECORD_NUMBER) d
        """;

    public const string StatusLotsAll = StatusLotsSelect + """

        WHERE m.NOUSE IS NULL
        ORDER BY c.WORKING_CODE, c.EXPIRED_DATE, c.LOTNO, c.RECORD_NUMBER
        """;

    public const string StatusLotsSearch = StatusLotsSelect + """

        WHERE m.NOUSE IS NULL
          AND (   m.DRUG_NAME    LIKE @Pattern
               OR m.COMPOSITION  LIKE @Pattern
               OR m.HOSP_CODE    LIKE @Pattern
               OR m.WORKING_CODE LIKE @Pattern)
        ORDER BY c.WORKING_CODE, c.EXPIRED_DATE, c.LOTNO, c.RECORD_NUMBER
        """;

    /// <summary>
    /// Active items (NOUSE IS NULL) grouped by ED/NED class. The page derives its headline totals by summing
    /// these groups, so one statement serves both the cards and the breakdown. LEFT JOIN keeps items whose
    /// ED_NED code has no lookup row (they appear with a NULL name) so the totals stay complete.
    /// </summary>
    public const string SummaryByEdNed = """
        SELECT m.ED_NED                       AS EdNedCode,
               e.EDNAME                       AS EdNedName,
               e.EDMAP                        AS EdNedShortName,
               COUNT(*)                       AS ItemCount,
               ISNULL(SUM(m.QTY_ON_HAND), 0)  AS TotalQtyOnHand,
               ISNULL(SUM(m.TOTAL_VALUE), 0)  AS TotalValue
        FROM dbo.INV_MD m
        LEFT JOIN dbo.TBLED_NED e ON e.EDCODE = m.ED_NED
        WHERE m.NOUSE IS NULL
        GROUP BY m.ED_NED, e.EDNAME, e.EDMAP
        ORDER BY m.ED_NED
        """;

    /// <summary>Item header for the detail page (active or inactive) with ED/NED name (legacy default.asp join).</summary>
    public static readonly string DetailHeader = $"""
        SELECT m.WORKING_CODE           AS WorkingCode,
               m.DRUG_NAME              AS DrugName,
               m.COMPOSITION            AS Composition,
               m.DOSAGE_FORM            AS DosageForm,
               m.SALE_UNIT              AS SaleUnit,
               m.LOCATION               AS Location,
               m.HOSP_CODE              AS HospCode,
               m.VEN                    AS Ven,
               m.ABC                    AS Abc,
               e.EDNAME                 AS EdNedName,
               CAST(CASE WHEN m.NOUSE IS NULL THEN 0 ELSE 1 END AS bit) AS IsInactive,
               ISNULL(m.QTY_ON_HAND, 0) AS QtyOnHand,
               ISNULL(m.TOTAL_VALUE, 0) AS TotalValue,
               m.RATE_PER_MONTH         AS RatePerMonth,
               CAST(ISNULL(bo.BORROWABLE, 0) AS decimal(14, 0)) AS BorrowableQty
        FROM dbo.INV_MD m
        LEFT JOIN dbo.TBLED_NED e ON e.EDCODE = m.ED_NED
        {BorrowableSubquery}
        WHERE m.WORKING_CODE = @WorkingCode
        """;

    /// <summary>
    /// Lots for one item, following chkstock.asp's join to DRUG_VN and COMPANY but without row multiplication.
    /// Ordered by expiry (soonest first), then lot number.
    /// </summary>
    public const string DetailLots = """
        SELECT c.PACK_RATIO   AS PackRatio,
               c.QTY_ON_HAND  AS QtyOnHand,
               c.EXPIRED_DATE AS ExpiredDate,
               c.LOTNO        AS LotNo,
               c.LOCATION     AS Location,
               c.LOT_VALUE    AS LotValue,
               c.VENDOR_CODE  AS VendorCode,
               cv.COMPANY_NAME AS VendorName,
               c.MANUFAC_CODE AS ManufacCode,
               cm.COMPANY_NAME AS ManufacName,
               d.TRADE_NAME   AS TradeName
        FROM dbo.INV_MD_C c
        OUTER APPLY (SELECT TOP 1 x.TRADE_NAME FROM dbo.DRUG_VN x
                     WHERE x.WORKING_CODE = c.WORKING_CODE AND x.PACK_RATIO = c.PACK_RATIO
                       AND x.VENDOR_CODE = c.VENDOR_CODE AND x.MANUFAC_CODE = c.MANUFAC_CODE
                     ORDER BY x.RECORD_NUMBER) d
        OUTER APPLY (SELECT TOP 1 y.COMPANY_NAME FROM dbo.COMPANY y
                     WHERE y.COMPANY_CODE = c.VENDOR_CODE ORDER BY y.RECORD_NUMBER) cv
        OUTER APPLY (SELECT TOP 1 z.COMPANY_NAME FROM dbo.COMPANY z
                     WHERE z.COMPANY_CODE = c.MANUFAC_CODE ORDER BY z.RECORD_NUMBER) cm
        WHERE c.WORKING_CODE = @WorkingCode
        ORDER BY c.EXPIRED_DATE, c.LOTNO, c.RECORD_NUMBER
        """;

    /// <summary>Set-based header-vs-lots reconciliation for every active item (parity A10).</summary>
    public const string LotTotals = """
        SELECT m.WORKING_CODE                AS WorkingCode,
               ISNULL(m.QTY_ON_HAND, 0)      AS HeaderQty,
               ISNULL(m.TOTAL_VALUE, 0)      AS HeaderValue,
               ISNULL(l.LotCount, 0)         AS LotCount,
               ISNULL(l.LotQtySum, 0)        AS LotQtySum,
               ISNULL(l.LotValueSum, 0)      AS LotValueSum
        FROM dbo.INV_MD m
        LEFT JOIN (
            SELECT WORKING_CODE,
                   COUNT(*)          AS LotCount,
                   SUM(QTY_ON_HAND)  AS LotQtySum,
                   SUM(LOT_VALUE)    AS LotValueSum
            FROM dbo.INV_MD_C
            GROUP BY WORKING_CODE
        ) l ON l.WORKING_CODE = m.WORKING_CODE
        WHERE m.NOUSE IS NULL
        ORDER BY m.WORKING_CODE
        """;
}
