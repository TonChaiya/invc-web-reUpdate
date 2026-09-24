using Invc.Core.Borrow;

namespace Invc.UnitTests;

/// <summary>
/// สรุปคงค้าง aggregation: facility × working code across bills, outstanding work only. Pure Core rules — the numbers
/// come from the existing read model (INV mirror + application-owned return balances), never from a new query.
/// </summary>
public class BorrowOutstandingSummaryTests
{
    private static BorrowItemView Item(int id, int billId, string code, string name, decimal borrowed, decimal returned) =>
        new(new BorrowSourceItem { SourceRecordNumber = id, SourceBillRecordNumber = billId, ReceiveNo = $"O{billId}", WorkingCode = code, DrugName = name, QtyOrder = borrowed },
            returned, null, 0, []);

    private static BorrowBillView Bill(int id, string receiveNo, DateTime date, params BorrowItemView[] items) =>
        new(new BorrowSourceBill { SourceRecordNumber = id, ReceiveNo = receiveNo, DateReceive = date, FacilityCode = "CUB001" }, items);

    private static BorrowFacilityView Facility(string code, string? name, params BorrowBillView[] bills) => new(code, name, bills);

    /// <summary>Owner's example: the same medicine on two bills of one facility becomes ONE row with summed figures.</summary>
    [Fact]
    public void Same_facility_and_working_code_across_bills_is_one_row()
    {
        var summary = BorrowOutstandingSummary.Build([
            Facility("CUB001", "ทรายมูล",
                Bill(10, "O6900010", new DateTime(2026, 7, 1), Item(1, 10, "1000010", "Acyclovir", 500, 100)),
                Bill(20, "O6900020", new DateTime(2026, 7, 20), Item(2, 20, "1000010", "Acyclovir", 300, 200)))]);

        var facility = Assert.Single(summary.Facilities);
        var drug = Assert.Single(facility.Drugs);
        Assert.Equal("1000010", drug.WorkingCode);
        Assert.Equal(2, drug.BillCount);
        Assert.Equal(800m, drug.BorrowedQty);
        Assert.Equal(300m, drug.ReturnedQty);
        Assert.Equal(500m, drug.OutstandingQty);
        // drill-down: newest receive first, per-bill figures preserved
        Assert.Equal(["O6900020", "O6900010"], drug.Bills.Select(b => b.ReceiveNo));
        Assert.Equal(100m, drug.Bills[0].OutstandingQty);
        Assert.Equal(400m, drug.Bills[1].OutstandingQty);
    }

    [Fact]
    public void Same_working_code_in_different_facilities_stays_separate()
    {
        var summary = BorrowOutstandingSummary.Build([
            Facility("CUB001", "ทรายมูล", Bill(10, "O1", new DateTime(2026, 7, 1), Item(1, 10, "1000010", "Acyclovir", 500, 0))),
            Facility("PAO001", "บ้านต้นเปา", Bill(30, "O3", new DateTime(2026, 7, 1), Item(3, 30, "1000010", "Acyclovir", 200, 0)))]);

        Assert.Equal(2, summary.Facilities.Count);
        Assert.All(summary.Facilities, f => Assert.Single(f.Drugs));
        Assert.Equal(2, summary.DrugCount);                                   // two pieces of work, not one medicine
        Assert.Equal(700m, summary.OutstandingQty);
        Assert.Equal("CUB001", summary.Facilities[0].FacilityCode);           // largest outstanding first
    }

    [Fact]
    public void Fully_returned_items_are_excluded_and_partial_returns_keep_the_remainder()
    {
        var summary = BorrowOutstandingSummary.Build([
            Facility("CUB001", "ทรายมูล", Bill(10, "O1", new DateTime(2026, 7, 1),
                Item(1, 10, "1000010", "Acyclovir", 500, 500),                 // fully returned → not work any more
                Item(2, 10, "1000020", "Albendazole", 400, 150),               // partial
                Item(3, 10, "1000030", "Conflict", 100, 180)))]);              // returned > borrowed → outstanding 0

        var facility = Assert.Single(summary.Facilities);
        var drug = Assert.Single(facility.Drugs);
        Assert.Equal("1000020", drug.WorkingCode);
        Assert.Equal(400m, drug.BorrowedQty);
        Assert.Equal(150m, drug.ReturnedQty);
        Assert.Equal(250m, drug.OutstandingQty);
    }

    [Fact]
    public void Facility_and_overall_totals_count_distinct_drugs_and_bills()
    {
        var summary = BorrowOutstandingSummary.Build([
            Facility("CUB001", "ทรายมูล",
                Bill(10, "O1", new DateTime(2026, 7, 1), Item(1, 10, "1000010", "Acyclovir", 500, 100), Item(2, 10, "1000020", "Albendazole", 300, 0)),
                Bill(20, "O2", new DateTime(2026, 7, 20), Item(3, 20, "1000010", "Acyclovir", 200, 0))),
            Facility("PAO001", "บ้านต้นเปา",
                Bill(30, "O3", new DateTime(2026, 6, 1), Item(4, 30, "3000860", "ซอง sterile", 5, 0)))]);

        var cub = summary.Facilities.Single(f => f.FacilityCode == "CUB001");
        Assert.Equal(2, cub.DrugCount);                                        // distinct working codes, not 3 source lines
        Assert.Equal(2, cub.BillCount);                                        // distinct bills, counted once
        Assert.Equal(1000m, cub.BorrowedQty);
        Assert.Equal(100m, cub.ReturnedQty);
        Assert.Equal(900m, cub.OutstandingQty);

        Assert.Equal(2, summary.FacilityCount);
        Assert.Equal(3, summary.DrugCount);                                    // 2 + 1 summary rows
        Assert.Equal(3, summary.BillCount);
        Assert.Equal(1005m, summary.BorrowedQty);
        Assert.Equal(905m, summary.OutstandingQty);
    }

    [Fact]
    public void Drugs_are_sorted_A_to_Z_and_facilities_by_outstanding_quantity()
    {
        var summary = BorrowOutstandingSummary.Build([
            Facility("CUB001", "ทรายมูล", Bill(10, "O1", new DateTime(2026, 7, 1),
                Item(1, 10, "9", "Zidovudine", 10, 0), Item(2, 10, "1", "Amoxicillin", 20, 0))),
            Facility("PAO001", "บ้านต้นเปา", Bill(30, "O3", new DateTime(2026, 7, 1), Item(3, 30, "5", "Metformin", 900, 0)))]);

        Assert.Equal(["PAO001", "CUB001"], summary.Facilities.Select(f => f.FacilityCode));
        Assert.Equal(["Amoxicillin", "Zidovudine"], summary.Facilities.Single(f => f.FacilityCode == "CUB001").Drugs.Select(d => d.DrugName));
    }

    /// <summary>Search filters before aggregation, so the displayed totals always describe the displayed rows.</summary>
    [Theory]
    [InlineData("Albendazole", "1000020", 300)]
    [InlineData("1000010", "1000010", 400)]
    [InlineData("O6900020", "1000010", 200)]                                   // a bill number keeps that bill's items
    public void Search_filters_before_aggregation(string keyword, string expectedCode, int expectedOutstanding)
    {
        var facilities = new[] {
            Facility("CUB001", "ทรายมูล",
                Bill(10, "O6900010", new DateTime(2026, 7, 1), Item(1, 10, "1000010", "Acyclovir", 500, 300), Item(2, 10, "1000020", "Albendazole", 300, 0)),
                Bill(20, "O6900020", new DateTime(2026, 7, 20), Item(3, 20, "1000010", "Acyclovir", 200, 0))) };

        var summary = BorrowOutstandingSummary.Build(facilities, keyword);
        var drug = Assert.Single(Assert.Single(summary.Facilities).Drugs);
        Assert.Equal(expectedCode, drug.WorkingCode);
        Assert.Equal(expectedOutstanding, drug.OutstandingQty);
        Assert.Equal(expectedOutstanding, summary.OutstandingQty);
    }

    [Fact]
    public void Searching_a_facility_keeps_all_of_its_outstanding_work()
    {
        var facilities = new[] {
            Facility("CUB001", "ทรายมูล", Bill(10, "O1", new DateTime(2026, 7, 1), Item(1, 10, "1000010", "Acyclovir", 500, 0), Item(2, 10, "1000020", "Albendazole", 300, 0))),
            Facility("PAO001", "บ้านต้นเปา", Bill(30, "O3", new DateTime(2026, 7, 1), Item(3, 30, "3000860", "ซอง sterile", 5, 0))) };

        var summary = BorrowOutstandingSummary.Build(facilities, "ทรายมูล");
        var facility = Assert.Single(summary.Facilities);
        Assert.Equal(2, facility.DrugCount);
        Assert.Equal(800m, summary.OutstandingQty);
    }

    [Fact]
    public void Nothing_outstanding_produces_an_empty_summary()
    {
        var summary = BorrowOutstandingSummary.Build([
            Facility("CUB001", "ทรายมูล", Bill(10, "O1", new DateTime(2026, 7, 1), Item(1, 10, "1000010", "Acyclovir", 500, 500)))]);
        Assert.True(summary.IsEmpty);
        Assert.Equal(0m, summary.OutstandingQty);
        Assert.Equal(0, summary.BillCount);
    }
}
