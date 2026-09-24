using Invc.Core.Inventory;

namespace Invc.Core.Borrow;

// ------------------------------------------------------------ สรุปคงค้าง ------------------------------------------------------------
// Review model for "what still has to be returned", aggregated per facility and working code instead of per bill,
// because the same medicine is borrowed on several bills of the same facility. It is derived in memory from the
// existing work board (mirror + return balances, two set-based MySQL queries) — no extra query per facility, per bill
// or per item, and no new table: the return state stays the application-owned event trail.
//
// Scope rule: only items that still have an outstanding quantity take part. Fully returned items are excluded, and so
// are conflict items (returned > borrowed ⇒ outstanding 0), which belong on the operational facility screen. All
// totals below are therefore totals *of the outstanding work*, not of the whole borrow history.

/// <summary>One source bill contributing to a drug's outstanding quantity (drill-down row).</summary>
public sealed record BorrowOutstandingBill(int SourceBillRecordNumber, string ReceiveNo, string? InvoiceNo, DateTime? DateReceive,
                                           decimal BorrowedQty, decimal ReturnedQty, decimal OutstandingQty);

/// <summary>One medicine of one facility, summed over every bill that still owes something.</summary>
public sealed record BorrowOutstandingDrug(string WorkingCode, string DrugName, IReadOnlyList<BorrowOutstandingBill> Bills)
{
    public int BillCount => Bills.Count;
    public decimal BorrowedQty => Bills.Sum(b => b.BorrowedQty);
    public decimal ReturnedQty => Bills.Sum(b => b.ReturnedQty);
    public decimal OutstandingQty => Bills.Sum(b => b.OutstandingQty);
}

/// <summary>One facility section. <see cref="DrugCount"/> counts distinct working codes, never raw source lines.</summary>
public sealed record BorrowOutstandingFacility(string FacilityCode, string? FacilityName, IReadOnlyList<BorrowOutstandingDrug> Drugs)
{
    public string DisplayName => string.IsNullOrWhiteSpace(FacilityName) ? FacilityCode : FacilityName!;
    public int DrugCount => Drugs.Count;
    /// <summary>Distinct bills of this facility that still owe something (one bill can hold several outstanding drugs).</summary>
    public int BillCount => Drugs.SelectMany(d => d.Bills).Select(b => b.SourceBillRecordNumber).Distinct().Count();
    public decimal BorrowedQty => Drugs.Sum(d => d.BorrowedQty);
    public decimal ReturnedQty => Drugs.Sum(d => d.ReturnedQty);
    public decimal OutstandingQty => Drugs.Sum(d => d.OutstandingQty);
}

/// <summary>
/// The whole สรุปคงค้าง view. <see cref="DrugCount"/> is the number of summary rows (facility × working code), which is
/// what the strip "รายการยา" means operationally: one medicine borrowed by two facilities is two pieces of work.
/// </summary>
public sealed record BorrowOutstandingSummary(IReadOnlyList<BorrowOutstandingFacility> Facilities)
{
    public int FacilityCount => Facilities.Count;
    public int DrugCount => Facilities.Sum(f => f.DrugCount);
    public int BillCount => Facilities.SelectMany(f => f.Drugs).SelectMany(d => d.Bills).Select(b => b.SourceBillRecordNumber).Distinct().Count();
    public decimal BorrowedQty => Facilities.Sum(f => f.BorrowedQty);
    public decimal ReturnedQty => Facilities.Sum(f => f.ReturnedQty);
    public decimal OutstandingQty => Facilities.Sum(f => f.OutstandingQty);
    public bool IsEmpty => Facilities.Count == 0;

    /// <summary>
    /// Aggregates the already-loaded work board. Ordering: facilities by outstanding quantity (largest first — the most
    /// work), ties by name; drugs A–Z by name (owner rule for every drug list); drill-down bills newest receive first,
    /// the same order the facility screen uses.
    ///
    /// <paramref name="keyword"/> filters before aggregation, so the totals always describe exactly what is displayed:
    /// a facility code/name match keeps all of that facility's outstanding items, a receive/invoice number match keeps
    /// that bill's items, otherwise the drug name or working code must match.
    /// </summary>
    public static BorrowOutstandingSummary Build(IEnumerable<BorrowFacilityView> facilities, string? keyword = null)
    {
        bool Has(string? s) => keyword is not null && s is not null && s.Contains(keyword, StringComparison.OrdinalIgnoreCase);

        var sections = new List<BorrowOutstandingFacility>();
        foreach (var facility in facilities)
        {
            var facilityMatches = keyword is null || Has(facility.FacilityCode) || Has(facility.FacilityName);
            var lines = new List<(BorrowBillView Bill, BorrowItemView Item)>();
            foreach (var bill in facility.Bills)
            {
                var billMatches = facilityMatches || Has(bill.Source.ReceiveNo) || Has(bill.Source.InvoiceNo);
                foreach (var item in bill.Items)
                {
                    if (item.OutstandingQty <= 0m) continue;                    // fully returned (and conflicts) are not work
                    if (!billMatches && !(Has(item.Source.DrugName) || Has(item.Source.WorkingCode))) continue;
                    lines.Add((bill, item));
                }
            }
            if (lines.Count == 0) continue;

            var drugs = lines
                .GroupBy(l => l.Item.Source.WorkingCode.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => new BorrowOutstandingDrug(
                    g.Key,
                    g.Select(l => l.Item.Source.DrugName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? g.Key,
                    g.GroupBy(l => l.Bill.Source.SourceRecordNumber)
                     .Select(bg => new BorrowOutstandingBill(bg.Key, bg.First().Bill.Source.ReceiveNo, bg.First().Bill.Source.InvoiceNo,
                         bg.First().Bill.Source.DateReceive,
                         bg.Sum(l => l.Item.BorrowedQty), bg.Sum(l => l.Item.ReturnedQty), bg.Sum(l => l.Item.OutstandingQty)))
                     .OrderByDescending(b => b.DateReceive).ThenByDescending(b => b.SourceBillRecordNumber)
                     .ToList()))
                .ToList();

            var sorted = DrugNameOrder.Sort(drugs, d => d.DrugName, d => d.WorkingCode);   // owner rule: A–Z by drug name
            sections.Add(new BorrowOutstandingFacility(facility.FacilityCode, facility.FacilityName, sorted));
        }

        return new BorrowOutstandingSummary(sections
            .OrderByDescending(f => f.OutstandingQty)
            .ThenBy(f => f.DisplayName, StringComparer.Ordinal)
            .ToList());
    }
}
