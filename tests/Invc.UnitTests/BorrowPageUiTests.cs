using System.Net;
using System.Text.RegularExpressions;
using Invc.Core.Borrow;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Invc.UnitTests;

/// <summary>/Borrow and /Borrow/Facility/{code} rendered through the real Razor pipeline with an in-memory source and mirror.</summary>
public sealed class BorrowPageUiTests : IClassFixture<BorrowPageUiTests.Factory>
{
    public sealed class Factory : WebApplicationFactory<Program>
    {
        public InMemoryMirror Mirror { get; } = new();
        public FakeSource Source { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("InvDatabase:ConnectionString", "Server=test;Initial Catalog=INV;Integrated Security=True");
            builder.UseSetting("AppDatabase:ConnectionString", "Server=127.0.0.1;Database=invc_web_test;User ID=root;Password=;");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IBorrowSourceRepository>();
                services.RemoveAll<IBorrowMirrorRepository>();
                services.AddSingleton<IBorrowSourceRepository>(Source);
                services.AddSingleton<IBorrowMirrorRepository>(Mirror);
            });
        }
    }

    public sealed class FakeSource : IBorrowSourceRepository
    {
        public bool Fail { get; set; }
        public IReadOnlyList<BorrowSourceBill> Bills { get; set; } =
        [
            new() { SourceRecordNumber = 113, ReceiveNo = "O6900084", InvoiceNo = "24082569", InvoiceDate = new DateTime(2026, 8, 24), DateReceive = new DateTime(2026, 8, 24), FacilityCode = "CUB001", FacilityName = "ศูนย์สาธารณสุขและการแพทย์ตำบลทรายมูล", SourceSysdate = new DateTime(2026, 9, 11, 9, 34, 52),
                Items = [ new() { SourceRecordNumber = 1036, SourceBillRecordNumber = 113, ReceiveNo = "O6900084", WorkingCode = "1001440", DrugName = "AMILORIDE + HCTZ (5+50MG) MODURETIC TAB", QtyOrder = 250, PackRatio = 250, LotNo = "808640.", ManufacCode = "CUB001", VendorCode = "CUB001" } ] },
            new() { SourceRecordNumber = 100, ReceiveNo = "O6900050", InvoiceNo = "1", InvoiceDate = new DateTime(2026, 7, 1), DateReceive = new DateTime(2026, 7, 1), FacilityCode = "CUB001", FacilityName = "ศูนย์สาธารณสุขและการแพทย์ตำบลทรายมูล",
                Items = [ new() { SourceRecordNumber = 900, SourceBillRecordNumber = 100, ReceiveNo = "O6900050", WorkingCode = "1000010", DrugName = "Acyclovir 400 mg tab", QtyOrder = 100, PackRatio = 100, LotNo = "L1" },
                          new() { SourceRecordNumber = 901, SourceBillRecordNumber = 100, ReceiveNo = "O6900050", WorkingCode = "1000020", DrugName = "Albendazole 200 mg tab", QtyOrder = 400, PackRatio = 100, LotNo = "L2" } ] },
            new() { SourceRecordNumber = 50, ReceiveNo = "O6900010", InvoiceNo = "9", DateReceive = new DateTime(2026, 3, 18), FacilityCode = "PAO001", FacilityName = "รพ.สต.บ้านต้นเปา",
                Items = [ new() { SourceRecordNumber = 500, SourceBillRecordNumber = 50, ReceiveNo = "O6900010", WorkingCode = "3000860", DrugName = "ซอง sterile 6 นิ้ว", QtyOrder = 5, PackRatio = 1, LotNo = null } ] },
        ];
        public Task<IReadOnlyList<BorrowSourceBill>> GetSnapshotAsync(CancellationToken cancellationToken = default)
            => Fail ? throw new InvalidOperationException("INV unreachable") : Task.FromResult(Bills);
    }

    public sealed class InMemoryMirror : IBorrowMirrorRepository
    {
        private readonly Dictionary<int, BorrowSourceBill> _bills = [];
        private readonly Dictionary<int, BorrowSourceItem> _items = [];
        public int ApplyCalls;
        public Task<BorrowMirrorState> GetStateAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new BorrowMirrorState(_bills.ToDictionary(k => k.Key, v => v.Value.ComputeHash()), _items.ToDictionary(k => k.Key, v => v.Value.ComputeHash())));
        public Task ApplyAsync(BorrowReconciliationPlan plan, DateTime syncedAt, CancellationToken cancellationToken = default)
        {
            ApplyCalls++;
            foreach (var b in plan.BillsToInsert.Concat(plan.BillsToUpdate)) _bills[b.SourceRecordNumber] = b with { Items = [] };
            foreach (var i in plan.ItemsToInsert.Concat(plan.ItemsToUpdate)) _items[i.SourceRecordNumber] = i;
            foreach (var id in plan.ItemsToDelete) _items.Remove(id);
            foreach (var id in plan.BillsToDelete) _bills.Remove(id);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<BorrowSourceBill>> GetBillsAsync(string? facilityCode, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<BorrowSourceBill> result = _bills.Values
                .Where(b => facilityCode is null || b.FacilityCode == facilityCode)
                .OrderByDescending(b => b.DateReceive).ThenByDescending(b => b.SourceRecordNumber)
                .Select(b => b with { Items = _items.Values.Where(i => i.SourceBillRecordNumber == b.SourceRecordNumber).OrderBy(i => i.SourceRecordNumber).ToList() })
                .ToList();
            return Task.FromResult(result);
        }
    }

    private readonly Factory _factory;
    private readonly HttpClient _client;
    public BorrowPageUiTests(Factory factory) { _factory = factory; _client = factory.CreateClient(); }

    private async Task<(HttpStatusCode Code, string Html)> GetAsync(string url)
    {
        var r = await _client.GetAsync(url);
        return (r.StatusCode, WebUtility.HtmlDecode(await r.Content.ReadAsStringAsync()));
    }

    private static string Text(string fragment) => Regex.Replace(Regex.Replace(fragment, "<[^>]+>", " "), @"\s+", " ").Trim();

    [Fact]
    public async Task Borrow_index_syncs_then_renders_summary_chips_and_a_semantic_facility_list()
    {
        _factory.Source.Fail = false;
        var (code, html) = await GetAsync("/Borrow");
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.Contains("ยายืมจากหน่วยงานอื่น", html);
        Assert.Contains("ซิงก์ INV", html);
        // summary: facilities 2 · bills 3 · items 4 · qty 755 — inline chips, not cards
        var summary = Regex.Match(html, "<div[^>]*class=\"[^\"]*borrow-summary[^\"]*\"[^>]*>(.*?)</div>", RegexOptions.Singleline);
        Assert.True(summary.Success, "borrow-summary expected");
        var text = Text(summary.Groups[1].Value);
        Assert.Contains("สถานบริการ 2", text); Assert.Contains("บิลที่เกี่ยวข้อง 3", text);
        Assert.Contains("รายการยืม 4", text); Assert.Contains("755 หน่วย", text);
        // facility list: semantic rows (no Bootstrap table), name primary + code secondary, numbers, link to detail
        var list = Regex.Match(html, "<div[^>]*class=\"[^\"]*borrow-facility-list[^\"]*\"[^>]*>(.*?)<details", RegexOptions.Singleline);
        Assert.True(list.Success, "borrow-facility-list expected");
        Assert.DoesNotContain("<table", list.Groups[1].Value);
        Assert.DoesNotContain("table-responsive", html);
        var rows = Regex.Matches(list.Groups[1].Value, "<a[^>]*class=\"[^\"]*borrow-facility-row[^\"]*\"[^>]*href=\"(/Borrow/Facility/[^\"]+)\"");
        Assert.Equal(2, rows.Count);
        Assert.Equal("/Borrow/Facility/CUB001", rows[0].Groups[1].Value);      // newest facility first
        Assert.Contains("class=\"borrow-facility-name\"", html);
        Assert.Contains("<span class=\"borrow-facility-code\">CUB001</span>", html);
        Assert.Contains("ศูนย์สาธารณสุขและการแพทย์ตำบลทรายมูล", html);
        Assert.DoesNotContain("inv-cards", html);
        Assert.DoesNotContain("class=\"card", html);
        Assert.True(_factory.Mirror.ApplyCalls >= 1);
    }

    [Fact]
    public async Task Borrow_index_search_filters_facilities_by_code_name_receive_no_or_drug()
    {
        _factory.Source.Fail = false;
        var (_, byName) = await GetAsync("/Borrow?q=ต้นเปา");
        Assert.Single(Regex.Matches(byName, "class=\"borrow-facility-row\""));
        Assert.Contains("PAO001", byName);
        var (_, byReceive) = await GetAsync("/Borrow?q=O6900084");
        Assert.Single(Regex.Matches(byReceive, "class=\"borrow-facility-row\""));
        Assert.Contains("CUB001", byReceive);
        var (_, byDrug) = await GetAsync("/Borrow?q=sterile");
        Assert.Single(Regex.Matches(byDrug, "class=\"borrow-facility-row\""));
        Assert.Contains("PAO001", byDrug);
        Assert.Contains("placeholder=\"ค้นหายา รหัสยา หรือเลขบิล\"", byName);
    }

    [Fact]
    public async Task Facility_page_renders_bill_groups_with_item_rows_and_no_nested_tables()
    {
        _factory.Source.Fail = false;
        var (code, html) = await GetAsync("/Borrow/Facility/CUB001");
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.Contains("ศูนย์สาธารณสุขและการแพทย์ตำบลทรายมูล", html);
        Assert.Matches("<span class=\"[^\"]*borrow-facility-code[^\"]*\">CUB001</span>", html);
        var summary = Text(Regex.Match(html, "<div[^>]*class=\"[^\"]*borrow-summary[^\"]*\"[^>]*>(.*?)</div>", RegexOptions.Singleline).Groups[1].Value);
        Assert.Matches(@"2\s*บิล", summary); Assert.Matches(@"3\s*รายการ", summary); Assert.Matches(@"750\s*หน่วย", summary);
        // bill groups newest first, semantic structure, no tables / overflow containers
        var bills = Regex.Matches(html, "<section[^>]*class=\"[^\"]*borrow-bill[^\"]*\"[^>]*>(.*?)</section>", RegexOptions.Singleline);
        Assert.Equal(2, bills.Count);
        Assert.Contains("O6900084", bills[0].Groups[1].Value);
        Assert.Contains("O6900050", bills[1].Groups[1].Value);
        Assert.All(bills.Cast<Match>(), b => Assert.Matches("<header[^>]*class=\"[^\"]*borrow-bill-header", b.Groups[1].Value));
        Assert.All(bills.Cast<Match>(), b => Assert.DoesNotContain("<table", b.Groups[1].Value));
        Assert.DoesNotContain("table-responsive", html);
        Assert.DoesNotContain("overflow-auto", html);
        Assert.Equal(3, Regex.Matches(html, "class=\"[^\"]*borrow-item-row[^\"]*\"").Count);
        Assert.Contains("class=\"borrow-item-name\"", html);
        Assert.Contains("class=\"borrow-item-meta\"", html);
        Assert.Contains("class=\"borrow-item-qty\"", html);
        Assert.Contains("เอกสาร 24082569", html);
        Assert.Contains("AMILORIDE + HCTZ (5+50MG) MODURETIC TAB", html);
        Assert.Contains("808640.", html);
        Assert.DoesNotContain("class=\"card", html);
        Assert.Contains("href=\"/Borrow\"", html);                              // breadcrumb back to the index
        // within-facility filter is real: drug/code/lot or bill number; totals in the header stay unfiltered
        var (_, filtered) = await GetAsync("/Borrow/Facility/CUB001?q=Acyclovir");
        Assert.Single(Regex.Matches(filtered, "class=\"[^\"]*borrow-item-row[^\"]*\""));
        Assert.Contains("Acyclovir 400 mg tab", filtered); Assert.DoesNotContain("AMILORIDE", filtered);
        Assert.Matches(@"3\s*รายการ", Text(Regex.Match(filtered, "<div[^>]*class=\"[^\"]*borrow-summary[^\"]*\"[^>]*>(.*?)</div>", RegexOptions.Singleline).Groups[1].Value));
        var (_, byBill) = await GetAsync("/Borrow/Facility/CUB001?q=O6900050");
        Assert.Equal(2, Regex.Matches(byBill, "class=\"[^\"]*borrow-item-row[^\"]*\"").Count);
        var (nf, _) = await GetAsync("/Borrow/Facility/NOPE99");
        Assert.Equal(HttpStatusCode.NotFound, nf);
        var (bad, _) = await GetAsync("/Borrow/Facility/x%27%3B--");
        Assert.Equal(HttpStatusCode.NotFound, bad);
    }

    [Fact]
    public async Task Borrow_index_keeps_showing_the_mirror_when_the_source_read_fails()
    {
        _factory.Source.Fail = false;
        await GetAsync("/Borrow");                       // populate the mirror
        var applied = _factory.Mirror.ApplyCalls;
        _factory.Source.Fail = true;
        try
        {
            var (code, html) = await GetAsync("/Borrow");
            Assert.Equal(HttpStatusCode.OK, code);
            Assert.Contains("ไม่สามารถซิงก์", html);         // sync warning
            Assert.Contains("CUB001", html);                  // mirror still rendered
            Assert.Equal(applied, _factory.Mirror.ApplyCalls);   // nothing applied/deleted
            Assert.DoesNotContain("SqlException", html);
        }
        finally { _factory.Source.Fail = false; }
    }
}
