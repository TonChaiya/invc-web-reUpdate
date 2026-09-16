using Invc.Core.PurchaseOrders;
using Invc.Infrastructure.Data;
using Invc.Infrastructure.PurchaseOrders;

namespace Invc.UnitTests;

public class PurchaseOrderRulesTests
{
    [Theory]
    [InlineData("1", true, false, false, false, false)]
    [InlineData("A", true, false, false, false, false)]
    [InlineData("B", true, false, false, false, false)]
    [InlineData("0", false, false, false, false, false)]
    [InlineData("C", false, false, false, false, false)]
    [InlineData("2", true, true, false, false, false)]
    [InlineData("3", true, true, false, false, false)]
    [InlineData("4", true, true, true, true, false)]
    [InlineData("5", true, true, true, true, true)]
    [InlineData("6", true, true, true, false, false)]
    [InlineData("7", true, true, true, true, false)]
    [InlineData("8", true, true, true, true, false)]
    [InlineData("9", true, true, true, true, true)]
    [InlineData("D", true, true, true, false, false)]
    [InlineData(null, false, false, false, false, false)]    // NULL status: legacy `NOT IN ('0','C')` is UNKNOWN → excluded from issued too
    [InlineData("", false, false, false, false, false)]
    [InlineData("Z", true, false, false, false, false)]
    public void Bucket_membership_matches_legacy_status_sets(string? status, bool issued, bool received, bool accounting, bool finance, bool closed)
    {
        Assert.Equal(issued, PurchaseOrderRules.IsInBucket(status, PurchaseOrderBucket.Issued));
        Assert.Equal(received, PurchaseOrderRules.IsInBucket(status, PurchaseOrderBucket.Received));
        Assert.Equal(accounting, PurchaseOrderRules.IsInBucket(status, PurchaseOrderBucket.Accounting));
        Assert.Equal(finance, PurchaseOrderRules.IsInBucket(status, PurchaseOrderBucket.Finance));
        Assert.Equal(closed, PurchaseOrderRules.IsInBucket(status, PurchaseOrderBucket.Closed));
        Assert.True(PurchaseOrderRules.IsInBucket(status, PurchaseOrderBucket.All));
    }

    [Fact]
    public void Status_sets_are_the_documented_legacy_sets()
    {
        Assert.Equal(new[] { "2", "3", "4", "5", "6", "7", "8", "9", "D" }, PurchaseOrderRules.ReceivedStatuses.Order());
        Assert.Equal(new[] { "4", "5", "6", "7", "8", "9", "D" }, PurchaseOrderRules.AccountingStatuses.Order());
        Assert.Equal(new[] { "4", "5", "7", "8", "9" }, PurchaseOrderRules.FinanceStatuses.Order());
        Assert.Equal(new[] { "5", "9" }, PurchaseOrderRules.ClosedStatuses.Order());
        Assert.Equal(new[] { "0", "C" }, PurchaseOrderRules.CancelledStatuses.Order());
    }

    [Theory]
    [InlineData("issued", PurchaseOrderBucket.Issued)]
    [InlineData("RECEIVED", PurchaseOrderBucket.Received)]
    [InlineData(" accounting ", PurchaseOrderBucket.Accounting)]
    [InlineData("finance", PurchaseOrderBucket.Finance)]
    [InlineData("closed", PurchaseOrderBucket.Closed)]
    [InlineData("all", PurchaseOrderBucket.All)]
    [InlineData(null, PurchaseOrderBucket.Issued)]
    [InlineData("", PurchaseOrderBucket.Issued)]
    [InlineData("purchase", PurchaseOrderBucket.Issued)]
    [InlineData("1; DROP TABLE MS_PO", PurchaseOrderBucket.Issued)]
    public void ParseBucket_defaults_to_issued(string? raw, PurchaseOrderBucket expected)
        => Assert.Equal(expected, PurchaseOrderRules.ParseBucket(raw));

    [Theory]
    [InlineData("6900001", 2569)]
    [InlineData("6800123", 2568)]
    [InlineData("70", 2570)]
    [InlineData("K6900001", null)]     // REAL_PO style — not a fiscal-year carrier
    [InlineData("6", null)]
    [InlineData(null, null)]
    [InlineData("", null)]
    public void FiscalYearFromPoNumber_uses_first_two_digits(string? poNo, int? expected)
        => Assert.Equal(expected, PurchaseOrderRules.FiscalYearFromPoNumber(poNo));

    [Theory]
    [InlineData(2569, "69")]
    [InlineData(2570, "70")]
    public void PoPrefix_round_trips_with_FiscalYearFromPoNumber(int fy, string prefix)
    {
        Assert.Equal(prefix, PurchaseOrderRules.PoPrefix(fy));
        Assert.Equal(fy, PurchaseOrderRules.FiscalYearFromPoNumber(prefix + "00001"));
    }

    [Theory]
    [InlineData("2569", 2569)]
    [InlineData(" 2570 ", 2570)]
    [InlineData("69", null)]
    [InlineData("2026", null)]
    [InlineData("abc", null)]
    [InlineData(null, null)]
    public void TryParseFiscalYear_accepts_only_buddhist_years_in_prefix_range(string? raw, int? expected)
        => Assert.Equal(expected, PurchaseOrderRules.TryParseFiscalYear(raw));

    [Theory]
    [InlineData(1000, 100, "10")]
    [InlineData(1000, 0, null)]
    [InlineData(1000, null, null)]
    [InlineData(null, 100, null)]
    [InlineData(250, 100, "2.5")]
    [InlineData(0, 100, "0")]
    public void Packs_guards_zero_and_null_ratio(int? qty, int? ratio, string? expected)
        => Assert.Equal(expected is null ? null : decimal.Parse(expected), PurchaseOrderRules.Packs(qty, ratio));

    [Fact]
    public void ProcessDays_is_days_since_first_receive_or_null()
    {
        var today = new DateTime(2026, 9, 16);
        Assert.Equal(10, PurchaseOrderRules.ProcessDays(new DateTime(2026, 9, 6, 15, 0, 0), today));
        Assert.Equal(0, PurchaseOrderRules.ProcessDays(today, today));
        Assert.Null(PurchaseOrderRules.ProcessDays(null, today));
    }

    [Theory]
    [InlineData("K6900001", true)]
    [InlineData("6900001", true)]
    [InlineData("PO-69/001", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("K69000011234", false)]    // > 10
    [InlineData("K69'--", false)]
    [InlineData("K69 001", false)]
    public void IsValidPoNumber(string? value, bool expected)
        => Assert.Equal(expected, PurchaseOrderRules.IsValidPoNumber(value));
}

public class PurchaseOrderReportTests
{
    private static PurchaseOrderSummary Po(string poNo, string? status, decimal? cost, string? statusName = "s", string? realPo = null)
        => new() { PoNo = poNo, RealPo = realPo ?? "K" + poNo, Status = status, TotalCost = cost, StatusName = statusName };

    private static readonly IReadOnlyList<PurchaseOrderSummary> Orders =
    [
        Po("6900001", "1", 100m),
        Po("6900002", "2", 200m),
        Po("6900003", "5", 300m),
        Po("6900004", "0", 400m),           // cancelled
        Po("6900005", "C", 500m),           // pending cancel
        Po("6900006", "D", 600m),
        Po("6900007", "X", 700m, null),     // unknown status, no lookup name
        Po("6900008", "9", null),           // null cost
    ];

    [Fact]
    public void Buckets_and_breakdown_derive_from_the_same_rows()
    {
        var r = PurchaseOrderReport.Build(2569, Orders, PurchaseOrderBucket.Issued);

        Assert.Equal(6, r.Buckets[PurchaseOrderBucket.Issued].Count);           // all but 0 and C
        Assert.Equal(100 + 200 + 300 + 600 + 700, r.Buckets[PurchaseOrderBucket.Issued].TotalValue);
        Assert.Equal(4, r.Buckets[PurchaseOrderBucket.Received].Count);         // 2,5,D,9
        Assert.Equal(3, r.Buckets[PurchaseOrderBucket.Accounting].Count);       // 5,D,9
        Assert.Equal(2, r.Buckets[PurchaseOrderBucket.Finance].Count);          // 5,9
        Assert.Equal(2, r.Buckets[PurchaseOrderBucket.Closed].Count);           // 5,9
        Assert.Equal(300m, r.Buckets[PurchaseOrderBucket.Closed].TotalValue);   // null cost counts as 0
        Assert.Equal(8, r.Buckets[PurchaseOrderBucket.All].Count);
        Assert.Equal(r.Buckets[PurchaseOrderBucket.Issued].Count, r.Rows.Count);
        Assert.Equal("69", r.PoPrefix);

        var x = r.StatusBreakdown.Single(s => s.Key == "X");
        Assert.Null(x.Label);
        Assert.Equal(1, x.Count);
        Assert.Equal(8, r.StatusBreakdown.Sum(s => s.Count));
        Assert.Equal(new[] { "0", "1", "2", "5", "9", "C", "D", "X" }, r.StatusBreakdown.Select(s => s.Key));
    }

    [Fact]
    public void All_bucket_is_superset_and_issued_excludes_cancelled_only()
    {
        var all = PurchaseOrderReport.Build(2569, Orders, PurchaseOrderBucket.All).Rows.Select(o => o.PoNo).ToList();
        var issued = PurchaseOrderReport.Build(2569, Orders, PurchaseOrderBucket.Issued).Rows.Select(o => o.PoNo).ToList();
        Assert.Equal(Orders.Select(o => o.PoNo), all);
        Assert.Equal(all.Except(new[] { "6900004", "6900005" }), issued);
    }

    [Fact]
    public void Summary_display_and_null_handling()
    {
        var po = new PurchaseOrderSummary { PoNo = "6900001" };
        Assert.Equal("6900001", po.DisplayNumber);
        Assert.Equal(2569, po.FiscalYear);
        Assert.False(po.HasStatusName);
        Assert.Null(po.ProcessDays(DateTime.Today));
        Assert.False(po.IsInBucket(PurchaseOrderBucket.Issued));   // NULL status is excluded, as the legacy NOT IN did
        Assert.True(po.IsInBucket(PurchaseOrderBucket.All));
        Assert.Equal("K6900001", Po("6900001", "1", 1).DisplayNumber);
    }

    [Fact]
    public void Reconciliation_compares_header_with_lines()
    {
        var header = new PurchaseOrderSummary { PoNo = "6900001", TotalItem = 2, TotalCost = 300m };
        var lines = new List<PurchaseOrderLine>
        {
            new() { WorkingCode = "1", BuyValue = 100m, QtyOrder = 1000, PackRatio1 = 100 },
            new() { WorkingCode = "2", BuyValue = 200m, QtyOrder = 10, PackRatio1 = 0 },
        };
        var d = new PurchaseOrderDetail { Header = header, Lines = lines, Receipts = [] };
        Assert.True(d.Reconciliation.IsConsistent);
        Assert.Equal(10m, lines[0].PacksOrdered);
        Assert.Null(lines[1].PacksOrdered);

        var off = new PurchaseOrderDetail { Header = header with { TotalCost = 310m, TotalItem = 3 }, Lines = lines, Receipts = [] };
        Assert.False(off.Reconciliation.TotalMatches);
        Assert.False(off.Reconciliation.ItemCountMatches);
        Assert.Equal(10m, off.Reconciliation.TotalDelta);

        var noHeader = new PurchaseOrderDetail { Header = new PurchaseOrderSummary { PoNo = "x" }, Lines = [], Receipts = [] };
        Assert.False(noHeader.Reconciliation.IsConsistent);
    }
}

public class DefaultFiscalYearTests
{
    private static readonly DateTime Today = new(2026, 9, 16);

    [Fact]
    public void Single_open_budget_year_wins()
    {
        Assert.Equal(2569, DefaultFiscalYear.Choose([2569], [2570, 2569], Today, out var note));
        Assert.Null(note);
    }

    [Fact]
    public void Multiple_open_years_pick_latest_and_flag_anomaly()
    {
        Assert.Equal(2570, DefaultFiscalYear.Choose([2569, 2570], [], Today, out var note));
        Assert.Contains("มากกว่าหนึ่งปี", note);
    }

    [Fact]
    public void No_open_year_falls_back_to_latest_po_year_then_current_fiscal_year()
    {
        Assert.Equal(2568, DefaultFiscalYear.Choose([], [2567, 2568], Today, out var note1));
        Assert.NotNull(note1);
        Assert.Equal(2569, DefaultFiscalYear.Choose([], [], Today, out var note2));   // Sep 2026 → FY 2569
        Assert.NotNull(note2);
    }
}

public class PurchaseOrderSqlTests
{
    [Fact]
    public void Every_statement_passes_the_read_only_guard()
    {
        var all = PurchaseOrderSql.AllStatements().ToList();
        Assert.Equal(9, all.Count);
        foreach (var sql in all)
        {
            Assert.Equal(sql, ReadOnlySql.Ensure(sql));
            Assert.DoesNotContain("SELECT *", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("OTH_IVO", sql, StringComparison.OrdinalIgnoreCase);   // non-PO receipts stay out of this module
        }
    }

    [Fact]
    public void Relationship_rules_are_encoded_exactly()
    {
        // Lines join on the internal PO_NO …
        Assert.Contains("WHERE l.PO_NO = @PoNo", PurchaseOrderSql.Lines(), StringComparison.Ordinal);
        // … receipts join on REAL_PO (parameter named RealPo), never on PO_NO …
        Assert.Contains("WHERE r.PO_NO = @RealPo", PurchaseOrderSql.Receipts(), StringComparison.Ordinal);
        Assert.DoesNotContain("@PoNo", PurchaseOrderSql.Receipts(), StringComparison.Ordinal);
        Assert.DoesNotContain("@PoNo", PurchaseOrderSql.ReceiptLines(), StringComparison.Ordinal);
        // … and receipt lines join MS_IVO on RECEIVE_NO.
        Assert.Contains("JOIN dbo.MS_IVO_C c ON c.RECEIVE_NO = r.RECEIVE_NO", PurchaseOrderSql.ReceiptLines(), StringComparison.Ordinal);
        Assert.Contains("FROM dbo.MS_IVO r", PurchaseOrderSql.Receipts(), StringComparison.Ordinal);
        Assert.Contains("FROM dbo.MS_PO_C l", PurchaseOrderSql.Lines(), StringComparison.Ordinal);
        Assert.Contains("WHERE p.REAL_PO = @RealPo", PurchaseOrderSql.HeaderByRealPo, StringComparison.Ordinal);
        // Fiscal year is a parameter, never concatenated.
        Assert.Contains("p.PO_NO LIKE @PrefixPattern", PurchaseOrderSql.HeadersByFiscalYear, StringComparison.Ordinal);
        Assert.DoesNotContain("LEFT(", PurchaseOrderSql.HeadersByFiscalYear, StringComparison.OrdinalIgnoreCase);
        // Lookup safety: no INNER JOIN in the header select; COMPANY via OUTER APPLY TOP 1.
        Assert.DoesNotContain("INNER JOIN", PurchaseOrderSql.HeadersByFiscalYear, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OUTER APPLY (SELECT TOP 1 c.COMPANY_NAME_PO", PurchaseOrderSql.HeadersByFiscalYear, StringComparison.Ordinal);
        // Status buckets are not in SQL.
        Assert.DoesNotContain("NOT IN ('0','C')", PurchaseOrderSql.HeadersByFiscalYear, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parameters_are_nvarchar_and_bounded()
    {
        var prefix = PurchaseOrderRepository.PrefixPattern("69");
        Assert.Equal("69%", prefix.Value);
        Assert.False(prefix.IsAnsi);
        var po = PurchaseOrderRepository.PoParam("K6900001");
        Assert.Equal(PurchaseOrderRules.PoNumberMaxLength, po.Length);
    }

    [Fact]
    public void AssembleReceipts_groups_lines_by_receive_no_and_handles_empty()
    {
        var receipts = new List<PurchaseOrderRepository.ReceiptRow>
        {
            new() { ReceiveNo = "R1", TotalCost = 10m },
            new() { ReceiveNo = "R2" },
        };
        var lines = new List<PurchaseOrderRepository.ReceiptLineRow>
        {
            new() { ReceiveNo = "R1", WorkingCode = "1000010", QtyOrder = 500, PackRatio1 = 100 },
            new() { ReceiveNo = "R1", WorkingCode = "1000020", QtyOrder = 5, PackRatio1 = 0 },
        };
        var result = PurchaseOrderRepository.AssembleReceipts(receipts, lines);
        Assert.Equal(2, result.Count);
        Assert.Equal(2, result[0].Lines.Count);
        Assert.Equal(5m, result[0].Lines[0].PacksReceived);
        Assert.Null(result[0].Lines[1].PacksReceived);
        Assert.Empty(result[1].Lines);
        Assert.Null(result[1].DateReceive);
        Assert.Empty(PurchaseOrderRepository.AssembleReceipts([], []));
    }
}
