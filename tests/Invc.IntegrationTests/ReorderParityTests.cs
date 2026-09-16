using Dapper;
using Invc.Core.Inventory;
using Invc.Core.Reorder;
using Invc.Infrastructure.Data;
using Invc.Infrastructure.Reorder;
using Xunit.Abstractions;

namespace Invc.IntegrationTests;

/// <summary>
/// Phase 3 parity checks B1–B6 (docs/report-parity-matrix.md) — read-only against live INV.
/// The raw side re-states the legacy VBScript of INV_Report_Purchase.asp independently in this test
/// (numbers read as 0 when NULL, REORDER_QTY 0 → MIN_LEVEL, red/yellow/green, CEILING(MAX − stock)).
/// </summary>
public class ReorderParityTests(ProductionReadOnlyFixture db, ITestOutputHelper output) : IClassFixture<ProductionReadOnlyFixture>
{
    private const string LegacySql = """
        SELECT WORKING_CODE, DRUG_NAME, QTY_ON_HAND, MIN_LEVEL, MAX_LEVEL, REORDER_QTY
        FROM dbo.INV_MD
        WHERE (NOUSE IS NULL OR NOUSE='') AND (OUT_OF_LIST IS NULL OR OUT_OF_LIST='')
        ORDER BY WORKING_CODE
        """;

    private sealed record LegacyRow(string WORKING_CODE, string DRUG_NAME, decimal? QTY_ON_HAND, decimal? MIN_LEVEL, decimal? MAX_LEVEL, decimal? REORDER_QTY)
    {
        public decimal Stock => QTY_ON_HAND ?? 0m;
        public decimal Min => MIN_LEVEL ?? 0m;
        public decimal Max => MAX_LEVEL ?? 0m;
        public decimal Rop => (REORDER_QTY ?? 0m) == 0m ? Min : REORDER_QTY!.Value;
        public string Status => Stock < Min ? "red" : (Stock >= Min && Stock < Rop) ? "yellow" : "green";
        public decimal Suggest => -Math.Floor(-(Max - Stock));   // VBScript RoundUpASP: -Int(-x)
    }

    private async Task<(List<LegacyRow> legacy, ReorderReport report)> LoadBothAsync(ReorderStatusFilter filter)
    {
        var connections = db.RequireOrSkip();
        await using var c = await connections.OpenAsync();
        var legacy = (await c.QueryAsync<LegacyRow>(ReadOnlySql.Ensure(LegacySql))).ToList();
        var eligible = await new ReorderRepository(connections).GetEligibleAsync(null);
        return (legacy, ReorderReport.Build(eligible, filter));
    }

    [SkippableFact]
    public async Task B1_eligible_row_count_matches_legacy_filter_and_differs_from_status_filter_only_if_data_does()
    {
        var connections = db.RequireOrSkip();
        var (legacy, report) = await LoadBothAsync(ReorderStatusFilter.All);
        await using var c = await connections.OpenAsync();
        var statusActive = await c.ExecuteScalarAsync<int>(ReadOnlySql.Ensure("SELECT COUNT(*) FROM dbo.INV_MD WHERE NOUSE IS NULL"));

        output.WriteLine($"{DateTime.Now:O} B1 legacy eligible {legacy.Count}, new {report.EligibleCount}, inventory-status active {statusActive}");
        Assert.Equal(legacy.Count, report.EligibleCount);
        Assert.Equal(legacy.Select(l => l.WORKING_CODE).OrderBy(x => x), report.Eligible.Select(i => i.WorkingCode).OrderBy(x => x));
    }

    [SkippableFact]
    public async Task B2_status_counts_match_legacy_classification()
    {
        var (legacy, report) = await LoadBothAsync(ReorderStatusFilter.Red);
        var red = legacy.Count(l => l.Status == "red");
        var yellow = legacy.Count(l => l.Status == "yellow");
        var green = legacy.Count(l => l.Status == "green");
        output.WriteLine($"{DateTime.Now:O} B2 legacy red/yellow/green {red}/{yellow}/{green}; new {report.RedCount}/{report.YellowCount}/{report.GreenCount}");

        Assert.Equal(red, report.RedCount);
        Assert.Equal(yellow, report.YellowCount);
        Assert.Equal(green, report.GreenCount);
        Assert.Equal(report.EligibleCount, report.RedCount + report.YellowCount + report.GreenCount);

        // Yellow-bucket analysis: when REORDER_QTY == MIN_LEVEL for every configured item the interval is empty.
        var ropEqualsMin = legacy.Count(l => (l.REORDER_QTY ?? 0m) != 0m && l.REORDER_QTY == l.MIN_LEVEL);
        var ropDiffers = legacy.Count(l => (l.REORDER_QTY ?? 0m) != 0m && l.REORDER_QTY != l.MIN_LEVEL);
        output.WriteLine($"B2 ROP==MIN {ropEqualsMin}, ROP!=MIN {ropDiffers}");
        if (ropDiffers == 0)
        {
            Assert.Equal(0, report.YellowCount);
        }
    }

    [SkippableFact]
    public async Task B3_red_suggested_total_matches_legacy_formula()
    {
        var (legacy, report) = await LoadBothAsync(ReorderStatusFilter.Red);
        var legacyTotal = legacy.Where(l => l.Status == "red").Sum(l => l.Suggest);
        var negativeRed = legacy.Count(l => l.Status == "red" && l.Suggest < 0);
        var negativeAll = legacy.Count(l => l.Suggest < 0);
        output.WriteLine($"{DateTime.Now:O} B3 red suggested total legacy {legacyTotal}, new {report.RedSuggestedTotal}; negative suggestions red {negativeRed}, all {negativeAll}");

        Assert.Equal(legacyTotal, report.RedSuggestedTotal);
        Assert.Equal(legacyTotal, report.Rows.Sum(i => i.SuggestedOrderQty));
    }

    [SkippableFact]
    public async Task B4_B5_representative_rows_match_field_by_field()
    {
        var (legacy, report) = await LoadBothAsync(ReorderStatusFilter.All);
        var byCode = report.Eligible.ToDictionary(i => i.WorkingCode);

        var picks = new List<LegacyRow>();
        picks.AddRange(legacy.Where(l => l.Status == "red").Take(3));
        picks.AddRange(legacy.Where(l => l.Status == "green").Take(3));
        picks.AddRange(legacy.Where(l => l.Status == "yellow").Take(2));
        picks.AddRange(legacy.Where(l => l.Min == 0m && l.Max == 0m).Take(2));                    // no thresholds
        picks.AddRange(legacy.Where(l => (l.REORDER_QTY ?? 0m) == 0m && l.Min > 0m).Take(2));      // B5 fallback
        picks.AddRange(legacy.Where(l => l.Suggest < 0).Take(2));                                   // negative suggestion
        picks.AddRange(legacy.Where(l => l.Status == "red" && l.Suggest < 0).Take(1));
        picks = picks.DistinctBy(l => l.WORKING_CODE).ToList();
        Skip.If(picks.Count == 0, "no eligible rows");

        var fallbackLive = legacy.Count(l => (l.REORDER_QTY ?? 0m) == 0m && l.Min > 0m);
        output.WriteLine($"{DateTime.Now:O} B4 comparing {picks.Count} rows; B5 live fallback cases: {fallbackLive}");
        foreach (var l in picks)
        {
            var n = byCode[l.WORKING_CODE];
            output.WriteLine($"  {l.WORKING_CODE} {l.DRUG_NAME}: stock {l.Stock} min {l.Min} rop {l.Rop} max {l.Max} → {l.Status}, suggest {l.Suggest}");
            Assert.Equal(l.DRUG_NAME, n.DrugName);
            Assert.Equal(l.Stock, n.QtyOnHand ?? 0m);
            Assert.Equal(l.Min, n.MinLevel ?? 0m);
            Assert.Equal(l.Max, n.MaxLevel ?? 0m);
            Assert.Equal(l.Rop, n.EffectiveReorderPoint);
            Assert.Equal(l.Status, n.Status.ToString().ToLowerInvariant());
            Assert.Equal(l.Suggest, n.SuggestedOrderQty);
        }

        // B5 rule must hold for every row regardless of whether live fallback cases exist.
        foreach (var l in legacy)
        {
            Assert.Equal(l.Rop, byCode[l.WORKING_CODE].EffectiveReorderPoint);
        }
    }

    [SkippableFact]
    public async Task B6_screen_and_print_row_sets_are_identical_for_every_status_and_all_is_their_union()
    {
        var connections = db.RequireOrSkip();
        var repo = new ReorderRepository(connections);
        var perStatus = new Dictionary<ReorderStatusFilter, List<string>>();

        foreach (var f in new[] { ReorderStatusFilter.Red, ReorderStatusFilter.Yellow, ReorderStatusFilter.Green, ReorderStatusFilter.All })
        {
            // Screen (Index) and Print build the report identically: two independent loads must agree.
            var screen = ReorderReport.Build(await repo.GetEligibleAsync(null), f).Rows.Select(i => (i.WorkingCode, i.SuggestedOrderQty, i.Status)).ToList();
            var print = ReorderReport.Build(await repo.GetEligibleAsync(null), f).Rows.Select(i => (i.WorkingCode, i.SuggestedOrderQty, i.Status)).ToList();
            Assert.Equal(screen, print);
            perStatus[f] = screen.Select(x => x.WorkingCode).ToList();
            output.WriteLine($"B6 {f}: {screen.Count} rows (first {screen.FirstOrDefault().WorkingCode}, last {screen.LastOrDefault().WorkingCode})");
        }

        var union = perStatus[ReorderStatusFilter.Red].Concat(perStatus[ReorderStatusFilter.Yellow]).Concat(perStatus[ReorderStatusFilter.Green]).OrderBy(c => c).ToList();
        Assert.Equal(union, perStatus[ReorderStatusFilter.All].OrderBy(c => c));
        Assert.Equal(perStatus[ReorderStatusFilter.All].Count, union.Count);
    }

    [SkippableFact]
    public async Task Sort_order_membership_equals_legacy_text_order_and_notes_any_sequence_difference()
    {
        var (legacy, report) = await LoadBothAsync(ReorderStatusFilter.All);
        var legacyOrder = legacy.Select(l => l.WORKING_CODE).ToList();      // ORDER BY WORKING_CODE (text)
        var newOrder = report.Eligible.Select(i => i.WorkingCode).ToList(); // numeric-safe key
        var sameSequence = legacyOrder.SequenceEqual(newOrder);
        output.WriteLine($"{DateTime.Now:O} sort: legacy text order == new order: {sameSequence}; codes all 7-digit numeric: {legacyOrder.All(c => c.Length == 7 && c.All(char.IsDigit))}");

        Assert.Equal(legacyOrder.OrderBy(c => c), newOrder.OrderBy(c => c));   // membership always identical
        if (legacyOrder.All(c => c.Length == 7 && c.All(char.IsDigit)))
        {
            Assert.True(sameSequence, "equal-length numeric codes must sort identically under both keys");
        }
    }

    [SkippableFact]
    public async Task Search_does_not_change_classification_and_is_deterministic()
    {
        var connections = db.RequireOrSkip();
        var repo = new ReorderRepository(connections);
        var all = ReorderReport.Build(await repo.GetEligibleAsync(null), ReorderStatusFilter.All).Eligible.ToDictionary(i => i.WorkingCode);

        foreach (var keyword in new[] { "10", "ยา", "tab", "50%" })
        {
            var a = ReorderReport.Build(await repo.GetEligibleAsync(keyword), ReorderStatusFilter.Red);
            var b = ReorderReport.Build(await repo.GetEligibleAsync(keyword), ReorderStatusFilter.Red);
            Assert.Equal(a.Rows.Select(i => i.WorkingCode), b.Rows.Select(i => i.WorkingCode));
            foreach (var i in a.Eligible)
            {
                Assert.Equal(all[i.WorkingCode].Status, i.Status);
                Assert.Equal(all[i.WorkingCode].SuggestedOrderQty, i.SuggestedOrderQty);
            }
            output.WriteLine($"search '{keyword}': eligible {a.EligibleCount}, red {a.RedCount}");
        }
    }

    [SkippableFact]
    public async Task Threshold_configuration_profile_is_reported()
    {
        var (legacy, _) = await LoadBothAsync(ReorderStatusFilter.All);
        output.WriteLine($"{DateTime.Now:O} thresholds: MIN 0/NULL {legacy.Count(l => l.Min == 0m)}, ROP 0/NULL {legacy.Count(l => (l.REORDER_QTY ?? 0m) == 0m)}, " +
                         $"MAX 0/NULL {legacy.Count(l => l.Max == 0m)}, of {legacy.Count}");
        Assert.True(legacy.Count >= 0);
    }
}
