using Invc.Core.Borrow;

namespace Invc.UnitTests;

/// <summary>Pure business rules of the return workflow (docs/borrow-workflow.md §Rules).</summary>
public class BorrowReturnRulesTests
{
    [Theory]
    [InlineData(100, 0, BorrowStatus.Open, 100)]
    [InlineData(100, 40, BorrowStatus.Partial, 60)]
    [InlineData(100, 100, BorrowStatus.Returned, 0)]
    [InlineData(0, 0, BorrowStatus.Returned, 0)]      // nothing borrowed = nothing outstanding
    public void Status_and_outstanding_follow_the_owner_rules(decimal borrowed, decimal returned, BorrowStatus status, decimal outstanding)
    {
        Assert.Equal(status, BorrowReturnRules.Status(borrowed, returned));
        Assert.Equal(outstanding, BorrowReturnRules.Outstanding(borrowed, returned));
        Assert.False(BorrowReturnRules.HasConflict(borrowed, returned));
    }

    [Fact]
    public void Source_quantity_lowered_below_returned_is_a_conflict_not_a_negative_outstanding()
    {
        // INV edited QTY_ORDER 100 → 30 after 50 were already returned
        Assert.True(BorrowReturnRules.HasConflict(30, 50));
        Assert.Equal(0m, BorrowReturnRules.Outstanding(30, 50));                 // clamped for display, flagged for review
        Assert.Equal(BorrowStatus.Returned, BorrowReturnRules.Status(30, 50));
        Assert.NotNull(BorrowReturnRules.ValidateReturn(30, 50, 1));            // no further returns until reviewed
        var view = new BorrowItemView(new BorrowSourceItem { SourceRecordNumber = 1, SourceBillRecordNumber = 1, ReceiveNo = "O1", WorkingCode = "1", QtyOrder = 30 }, 50, null, 2, []);
        Assert.True(view.HasConflict); Assert.False(view.CanReturn);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Return_rejects_non_positive_quantity(decimal qty) => Assert.Equal("จำนวนที่คืนต้องมากกว่า 0", BorrowReturnRules.ValidateReturn(100, 0, qty));

    [Fact]
    public void Return_rejects_over_return_fractions_and_already_returned_items()
    {
        Assert.Equal("คืนได้ไม่เกินยอดคงเหลือ 60", BorrowReturnRules.ValidateReturn(100, 40, 61));
        Assert.Null(BorrowReturnRules.ValidateReturn(100, 40, 60));
        Assert.Equal("จำนวนที่คืนต้องเป็นจำนวนเต็ม", BorrowReturnRules.ValidateReturn(100, 0, 1.5m));
        Assert.Equal("รายการนี้คืนครบแล้ว", BorrowReturnRules.ValidateReturn(100, 100, 1));
    }

    [Fact]
    public void Correction_requires_reason_and_stays_within_reversible_and_net_quantities()
    {
        Assert.Equal("ต้องระบุเหตุผลในการแก้ไข", BorrowReturnRules.ValidateCorrection(5, 10, 10, " "));
        Assert.Equal("ย้อนกลับได้ไม่เกิน 10", BorrowReturnRules.ValidateCorrection(11, 10, 50, "ผิด"));
        Assert.Equal("รายการคืนนี้ถูกย้อนกลับครบแล้ว", BorrowReturnRules.ValidateCorrection(1, 0, 50, "ผิด"));
        Assert.Equal("ยอดคืนสุทธิของรายการจะติดลบ", BorrowReturnRules.ValidateCorrection(10, 10, 5, "ผิด"));   // other corrections already reduced the net
        Assert.Null(BorrowReturnRules.ValidateCorrection(10, 10, 10, "บันทึกผิด"));
    }

    [Fact]
    public void Bill_status_derives_from_items()
    {
        Assert.Equal(BorrowStatus.Open, BorrowReturnRules.AggregateStatus([(100m, 0m), (50m, 0m)]));
        Assert.Equal(BorrowStatus.Partial, BorrowReturnRules.AggregateStatus([(100m, 100m), (50m, 0m)]));   // one item fully returned, another open
        Assert.Equal(BorrowStatus.Partial, BorrowReturnRules.AggregateStatus([(100m, 10m)]));
        Assert.Equal(BorrowStatus.Returned, BorrowReturnRules.AggregateStatus([(100m, 100m), (50m, 50m)]));
    }

    [Fact]
    public void Workboard_totals_and_facility_ordering_put_outstanding_work_first()
    {
        BorrowSourceItem I(int rn, int bill, string code, decimal qty, string name) => new() { SourceRecordNumber = rn, SourceBillRecordNumber = bill, ReceiveNo = "O" + bill, WorkingCode = code, DrugName = name, QtyOrder = qty };
        var bills = new List<BorrowSourceBill>
        {
            new() { SourceRecordNumber = 1, ReceiveNo = "O1", FacilityCode = "A", FacilityName = "Alpha", DateReceive = new DateTime(2026, 9, 1), Items = [I(11, 1, "1", 100, "Zeta"), I(12, 1, "2", 50, "Beta")] },
            new() { SourceRecordNumber = 2, ReceiveNo = "O2", FacilityCode = "B", FacilityName = "Bravo", DateReceive = new DateTime(2026, 9, 10), Items = [I(21, 2, "3", 10, "Gamma")] },
        };
        var balances = new[] { new BorrowItemBalance(11, 100, new DateTime(2026, 9, 5), 1), new BorrowItemBalance(21, 10, new DateTime(2026, 9, 11), 1) };
        var board = BorrowWorkboard.Build(bills, balances);
        Assert.Equal(2, board.FacilityCount); Assert.Equal(2, board.BillCount); Assert.Equal(3, board.ItemCount);
        Assert.Equal(160m, board.BorrowedQty); Assert.Equal(110m, board.ReturnedQty); Assert.Equal(50m, board.OutstandingQty);
        Assert.Equal(1, board.OpenBillCount);
        Assert.Equal("A", board.Facilities[0].FacilityCode);                       // outstanding first even though B is newer
        Assert.Equal(BorrowStatus.Partial, board.Facilities[0].Status);
        Assert.Equal(BorrowStatus.Returned, board.Facilities[1].Status);
        Assert.Equal(["Beta", "Zeta"], board.Facilities[0].Bills[0].Items.Select(i => i.Source.DrugName));   // A–Z inside the bill
        Assert.True(BorrowWorkboard.Matches(BorrowStatusFilter.Outstanding, BorrowStatus.Partial, false));
        Assert.False(BorrowWorkboard.Matches(BorrowStatusFilter.Returned, BorrowStatus.Returned, hasConflict: true));   // conflicts never count as done
        Assert.True(BorrowWorkboard.Matches(BorrowStatusFilter.Outstanding, BorrowStatus.Returned, hasConflict: true));
    }

    [Fact]
    public void Receipt_hints_attach_only_to_outstanding_items_and_only_on_or_after_the_borrow_date()
    {
        var bill = new BorrowSourceBill { SourceRecordNumber = 1, ReceiveNo = "O1", FacilityCode = "A", DateReceive = new DateTime(2026, 6, 10),
            Items = [new() { SourceRecordNumber = 11, SourceBillRecordNumber = 1, ReceiveNo = "O1", WorkingCode = "1001620", QtyOrder = 100 }, new() { SourceRecordNumber = 12, SourceBillRecordNumber = 1, ReceiveNo = "O1", WorkingCode = "1000510", QtyOrder = 10 }] };
        var hints = new[]
        {
            new BorrowReceiptHint("O9", "11", "CUP", new DateTime(2026, 6, 20), "1001620", 300, 300, "L1"),
            new BorrowReceiptHint("O8", "11", "CUP", new DateTime(2026, 6, 1), "1001620", 300, 300, "L0"),     // before the borrow → excluded
            new BorrowReceiptHint("O7", "10", "CUP", new DateTime(2026, 7, 1), "1000510", 5, 30, null),         // item fully returned → no hint
        };
        var views = BorrowWorkboard.BuildBills([bill], [new BorrowItemBalance(12, 10, null, 1)], hints);
        var losartan = views[0].Items.Single(i => i.Source.WorkingCode == "1001620");
        Assert.Equal(["O9"], losartan.ReceiptHints.Select(h => h.ReceiveNo));
        Assert.Empty(views[0].Items.Single(i => i.Source.WorkingCode == "1000510").ReceiptHints);
    }

    [Fact]
    public void Actor_is_the_authenticated_name_or_a_marked_server_side_fallback()
    {
        Assert.Equal("DESKTOP\\nurse", BorrowReturnRules.ResolveActor("DESKTOP\\nurse", "10.0.0.5"));
        Assert.Equal("anonymous:10.0.0.5", BorrowReturnRules.ResolveActor(null, "10.0.0.5"));
        Assert.Equal("anonymous:?", BorrowReturnRules.ResolveActor("  ", null));
        Assert.Equal(BorrowReturnRules.MaxActorLength, BorrowReturnRules.ResolveActor(new string('x', 300), null).Length);
    }

    [Fact]
    public void Status_filter_query_values_round_trip_and_default_to_outstanding()
    {
        foreach (var f in Enum.GetValues<BorrowStatusFilter>()) Assert.Equal(f, BorrowStatusFilterExtensions.Parse(f.ToQueryValue()));
        Assert.Equal(BorrowStatusFilter.Outstanding, BorrowStatusFilterExtensions.Parse(null));
        Assert.Equal(BorrowStatusFilter.Outstanding, BorrowStatusFilterExtensions.Parse("garbage"));
    }
}
