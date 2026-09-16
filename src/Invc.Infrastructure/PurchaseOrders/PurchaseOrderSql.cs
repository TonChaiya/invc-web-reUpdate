namespace Invc.Infrastructure.PurchaseOrders;

/// <summary>
/// SQL for the actual purchase-order module (MS_PO / MS_PO_C / MS_IVO / MS_IVO_C), ported from PO_Search.asp,
/// PODetail.asp and table_ipiss.asp (docs/legacy-query-map.md, docs/purchase-order-business-rules.md).
///
/// Durable relationship rules (never swap them):
///   MS_PO_C.PO_NO   = MS_PO.PO_NO      (internal number)
///   MS_IVO.PO_NO    = MS_PO.REAL_PO    (document number)
///   MS_IVO_C.RECEIVE_NO = MS_IVO.RECEIVE_NO
///
/// Deliberate corrections of legacy defects (docs/phase4-purchase-order-parity.md):
///  * lookups are LEFT JOIN (TblPOStatus, BDG_TYPE, TBLBUY, TBLED_NED) or OUTER APPLY TOP 1 (COMPANY, whose
///    COMPANY_CODE is not unique) so a PO never disappears or multiplies because of lookup data;
///  * fiscal year and search values are parameters, never concatenated;
///  * status buckets are NOT encoded in SQL — every PO of the year is read once and classified in Core.
/// </summary>
internal static class PurchaseOrderSql
{
    private const string VendorApply = """
        OUTER APPLY (SELECT TOP 1 c.COMPANY_NAME_PO, c.COMPANY_NAME FROM dbo.COMPANY c
                     WHERE c.COMPANY_CODE = p.VENDOR_CODE ORDER BY c.RECORD_NUMBER) v
        """;

    private const string HeaderSelect = $"""
        SELECT p.PO_NO          AS PoNo,
               p.REAL_PO        AS RealPo,
               p.DOC_NO         AS DocNo,
               p.PO_DATE        AS PoDate,
               p.STATUS         AS Status,
               s.StatusName     AS StatusName,
               p.VENDOR_CODE    AS VendorCode,
               ISNULL(v.COMPANY_NAME_PO, v.COMPANY_NAME) AS VendorName,
               p.BUDGET_TYPE    AS BudgetType,
               b.BDGNAME        AS BudgetName,
               p.BUY_METHOD     AS BuyMethod,
               t.BUYNAME        AS BuyMethodName,
               p.ED_NED         AS EdNed,
               e.EDMAP          AS EdNedName,
               p.TOTAL_ITEM     AS TotalItem,
               p.TOTAL_COST     AS TotalCost,
               p.FIRST_RCVDATE  AS FirstReceiveDate,
               p.BILLIN         AS BillIn,
               p.BILLOUT        AS BillOut,
               p.BILLEND        AS BillEnd,
               p.BILLOUTACC     AS BillOutAcc,
               p.BILLOUTFIN     AS BillOutFin,
               p.BILL_END_ACC   AS BillEndAcc,
               p.APPROVE_NO_FIN AS ApproveNoFin,
               p.BILL_PAY_FIN   AS BillPayFin
        FROM dbo.MS_PO p
        LEFT JOIN dbo.TblPOStatus s ON s.StatusCode = p.STATUS
        LEFT JOIN dbo.BDG_TYPE    b ON b.BDGCODE    = p.BUDGET_TYPE
        LEFT JOIN dbo.TBLBUY      t ON t.BUYCODE    = p.BUY_METHOD
        LEFT JOIN dbo.TBLED_NED   e ON e.EDCODE     = p.ED_NED
        {VendorApply}
        """;

    /// <summary>Legacy fiscal-year rule: LEFT(PO_NO, 2) = prefix — expressed as a sargable LIKE on a parameter.</summary>
    private const string FiscalYearFilter = "WHERE p.PO_NO LIKE @PrefixPattern";

    private const string HeaderOrder = "ORDER BY p.REAL_PO, p.PO_NO";

    public const string HeadersByFiscalYear = $"""
        {HeaderSelect}
        {FiscalYearFilter}
        {HeaderOrder}
        """;

    public const string HeadersByFiscalYearSearch = $"""
        {HeaderSelect}
        {FiscalYearFilter}
          AND (   p.REAL_PO LIKE @Pattern
               OR p.PO_NO   LIKE @Pattern
               OR p.DOC_NO  LIKE @Pattern
               OR v.COMPANY_NAME_PO LIKE @Pattern
               OR v.COMPANY_NAME    LIKE @Pattern)
        {HeaderOrder}
        """;

    public const string HeaderByRealPo = $"""
        {HeaderSelect}
        WHERE p.REAL_PO = @RealPo
        """;

    /// <summary>MS_PO_C lines: PO_NO = MS_PO.PO_NO. Drug name via OUTER APPLY on the unique WORKING_CODE (no multiplication).
    /// Source parameters carry their alias (l / r / c) so tests can substitute table-value constructors.</summary>
    public static string Lines(string linesSource = "dbo.MS_PO_C l") => $"""
        SELECT l.RUNNO         AS RunNo,
               l.WORKING_CODE  AS WorkingCode,
               d.DRUG_NAME     AS DrugName,
               l.DRUG_PO       AS DrugPo,
               l.QTY_ORDER     AS QtyOrder,
               l.PACK_RATIO1   AS PackRatio1,
               l.PO_UNIT       AS PoUnit,
               l.BUY_UNIT_COST AS BuyUnitCost,
               l.BUY_VALUE     AS BuyValue,
               l.QTY_FREE      AS QtyFree,
               l.PACK_RATIO2   AS PackRatio2,
               l.PO_C_NOTE     AS Note
        FROM {linesSource}
        OUTER APPLY (SELECT TOP 1 m.DRUG_NAME FROM dbo.INV_MD m WHERE m.WORKING_CODE = l.WORKING_CODE ORDER BY m.RECORD_NUMBER) d
        WHERE l.PO_NO = @PoNo
        ORDER BY l.RUNNO, l.RECORD_NUMBER
        """;

    /// <summary>MS_IVO receipts: PO_NO = MS_PO.REAL_PO (NOT PO_NO).</summary>
    public static string Receipts(string receiptsSource = "dbo.MS_IVO r") => $"""
        SELECT r.RECEIVE_NO   AS ReceiveNo,
               r.INVOICE_NO   AS InvoiceNo,
               r.INVOICE_DATE AS InvoiceDate,
               r.DATE_RECEIVE AS DateReceive,
               r.TOTAL_COST   AS TotalCost,
               r.TOTAL_ITEM   AS TotalItem,
               r.DATE_ACC     AS DateAcc
        FROM {receiptsSource}
        WHERE r.PO_NO = @RealPo
        ORDER BY r.DATE_RECEIVE, r.RECEIVE_NO, r.RECORD_NUMBER
        """;

    /// <summary>MS_IVO_C lines for all receipts of the PO in one statement (RECEIVE_NO = MS_IVO.RECEIVE_NO).</summary>
    public static string ReceiptLines(string receiptsSource = "dbo.MS_IVO r", string receiptLinesSource = "dbo.MS_IVO_C c") => $"""
        SELECT c.RECEIVE_NO    AS ReceiveNo,
               c.WORKING_CODE  AS WorkingCode,
               d.DRUG_NAME     AS DrugName,
               c.QTY_ORDER     AS QtyOrder,
               c.PACK_RATIO1   AS PackRatio1,
               c.BUY_UNIT_COST AS BuyUnitCost,
               c.QTY_FREE      AS QtyFree,
               c.PACK_RATIO2   AS PackRatio2,
               c.EXPIRED_DATE1 AS ExpiredDate1,
               c.LOCATION1     AS Location1,
               c.LOTNO         AS LotNo,
               c.MANUFAC_CODE  AS ManufacCode
        FROM {receiptsSource}
        JOIN {receiptLinesSource} ON c.RECEIVE_NO = r.RECEIVE_NO
        OUTER APPLY (SELECT TOP 1 m.DRUG_NAME FROM dbo.INV_MD m WHERE m.WORKING_CODE = c.WORKING_CODE ORDER BY m.RECORD_NUMBER) d
        WHERE r.PO_NO = @RealPo
        ORDER BY r.RECEIVE_NO, c.RECORD_NUMBER
        """;

    public const string OpenBudgetYears = """
        SELECT DISTINCT [year] AS BudgetYear FROM dbo.BUDGET WHERE BudgetOpen = 'O' ORDER BY [year]
        """;

    public const string PoFiscalYearPrefixes = """
        SELECT DISTINCT LEFT(PO_NO, 2) AS Prefix FROM dbo.MS_PO WHERE PO_NO IS NOT NULL AND LEN(PO_NO) >= 2 ORDER BY LEFT(PO_NO, 2) DESC
        """;

    /// <summary>Legacy PO_Search.asp date list (typed parameter instead of string dates).</summary>
    public const string BillOutDates = """
        SELECT DISTINCT CAST(BILLOUT AS date) AS BillOutDate FROM dbo.MS_PO
        WHERE PO_NO LIKE @PrefixPattern AND BILLOUT IS NOT NULL ORDER BY CAST(BILLOUT AS date) DESC
        """;

    /// <summary>Header statements captured for the read-only guard regression test.</summary>
    public static IEnumerable<string> AllStatements()
    {
        yield return HeadersByFiscalYear;
        yield return HeadersByFiscalYearSearch;
        yield return HeaderByRealPo;
        yield return Lines();
        yield return Receipts();
        yield return ReceiptLines();
        yield return OpenBudgetYears;
        yield return PoFiscalYearPrefixes;
        yield return BillOutDates;
    }
}
