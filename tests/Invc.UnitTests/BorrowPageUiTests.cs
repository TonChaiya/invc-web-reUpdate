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
        public InMemoryReturns Returns { get; }
        public FakeAdvisory Advisory { get; } = new();
        public Factory() { Returns = new InMemoryReturns(Mirror); }

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
                services.RemoveAll<IBorrowReturnRepository>();
                services.AddSingleton<IBorrowReturnRepository>(Returns);
                services.RemoveAll<IBorrowReceiptAdvisoryRepository>();
                services.AddSingleton<IBorrowReceiptAdvisoryRepository>(Advisory);
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

    public sealed class FakeAdvisory : IBorrowReceiptAdvisoryRepository
    {
        public bool Fail { get; set; }
        public Task<IReadOnlyList<BorrowReceiptHint>> GetNonBorrowReceiptsAsync(IReadOnlyCollection<string> workingCodes, DateTime since, CancellationToken cancellationToken = default)
            => Fail ? throw new InvalidOperationException("INV unreachable")
                    : Task.FromResult<IReadOnlyList<BorrowReceiptHint>>([new BorrowReceiptHint("O6900085", "11", "รายการ (ยา) ที่เบิกจากแม่ข่าย CUP", new DateTime(2026, 9, 2), "1001440", 500, 250, "808999")]);
    }

    /// <summary>In-memory append-only trail with the same validation as the MySQL repository (rules are shared in Core).</summary>
    public sealed class InMemoryReturns(InMemoryMirror mirror) : IBorrowReturnRepository
    {
        public readonly List<BorrowReturnEvent> Events = [];
        public readonly Dictionary<string, long> ClientRequests = [];
        public bool Fail { get; set; }
        private long _next = 1;
        private void Guard() { if (Fail) throw new TimeoutException("MySQL down (simulated connection failure)"); }
        private (BorrowSourceBill Bill, BorrowSourceItem Item)? Find(int itemRn)
        {
            foreach (var b in mirror.GetBillsAsync(null).Result) foreach (var i in b.Items) if (i.SourceRecordNumber == itemRn) return (b, i);
            return null;
        }
        private decimal Net(int itemRn) => Events.Where(e => e.SourceItemRecordNumber == itemRn).Sum(e => e.QuantityDelta);
        private BorrowWriteResult? Dup(string? cid) => cid is not null && ClientRequests.TryGetValue(cid, out var id) ? BorrowWriteResult.Duplicate(id) : null;
        public Task<IReadOnlyList<BorrowItemBalance>> GetBalancesAsync(string? facilityCode, CancellationToken cancellationToken = default)
        {
            Guard();
            IReadOnlyList<BorrowItemBalance> r = Events.Where(e => facilityCode is null || e.FacilityCode == facilityCode).GroupBy(e => e.SourceItemRecordNumber)
                .Select(g => new BorrowItemBalance(g.Key, g.Sum(e => e.QuantityDelta), g.Where(e => e.EventType == BorrowReturnEventType.Return).Max(e => (DateTime?)e.EventAt), g.Count())).ToList();
            return Task.FromResult(r);
        }
        public Task<BorrowWriteResult> RecordReturnAsync(BorrowReturnRequest request, CancellationToken cancellationToken = default)
        {
            Guard();
            if (Dup(request.ClientRequestId) is { } d) return Task.FromResult(d);
            if (Find(request.SourceItemRecordNumber) is not { } f) return Task.FromResult(BorrowWriteResult.Fail("ไม่พบรายการยืมนี้ในข้อมูลที่ซิงก์ไว้"));
            if (f.Bill.SourceRecordNumber != request.SourceBillRecordNumber) return Task.FromResult(BorrowWriteResult.Fail("รายการไม่ตรงกับบิลที่ระบุ"));
            if (BorrowReturnRules.ValidateReturn(f.Item.QtyOrder ?? 0m, Net(request.SourceItemRecordNumber), request.Quantity) is { } err) return Task.FromResult(BorrowWriteResult.Fail(err));
            var id = Add(f.Bill, f.Item, BorrowReturnEventType.Return, request.Quantity, request.EventAt, request.Actor, request.Note, null);
            if (request.ClientRequestId is not null) ClientRequests[request.ClientRequestId] = id;
            return Task.FromResult(BorrowWriteResult.Ok(id));
        }
        public Task<BorrowWriteResult> RecordBillReturnAsync(int sourceBillRecordNumber, DateTime eventAt, string actor, string? note, string? clientRequestId, CancellationToken cancellationToken = default)
        {
            Guard();
            if (Dup(clientRequestId) is { } d) return Task.FromResult(d);
            var bill = mirror.GetBillsAsync(null).Result.FirstOrDefault(b => b.SourceRecordNumber == sourceBillRecordNumber);
            if (bill is null) return Task.FromResult(BorrowWriteResult.Fail("ไม่พบบิลนี้ในข้อมูลที่ซิงก์ไว้"));
            var ids = new List<long>();
            foreach (var i in bill.Items)
            {
                var outstanding = BorrowReturnRules.Outstanding(i.QtyOrder ?? 0m, Net(i.SourceRecordNumber));
                if (outstanding <= 0m) continue;
                ids.Add(Add(bill, i, BorrowReturnEventType.Return, outstanding, eventAt, actor, note, null));
            }
            if (ids.Count == 0) return Task.FromResult(BorrowWriteResult.Fail("บิลนี้ไม่มีรายการคงค้าง"));
            if (clientRequestId is not null) ClientRequests[clientRequestId] = ids[0];
            return Task.FromResult(BorrowWriteResult.Ok([.. ids]));
        }
        public Task<BorrowWriteResult> RecordCorrectionAsync(BorrowCorrectionRequest request, CancellationToken cancellationToken = default)
        {
            Guard();
            if (Dup(request.ClientRequestId) is { } d) return Task.FromResult(d);
            var ev = Events.FirstOrDefault(e => e.Id == request.CorrectsEventId);
            if (ev is null) return Task.FromResult(BorrowWriteResult.Fail("ไม่พบรายการคืนที่ต้องการแก้ไข"));
            if (ev.EventType != BorrowReturnEventType.Return) return Task.FromResult(BorrowWriteResult.Fail("แก้ไขได้เฉพาะรายการคืน (ไม่ใช่รายการแก้ไข)"));
            var reversible = ev.QuantityDelta + Events.Where(e => e.CorrectsEventId == ev.Id).Sum(e => e.QuantityDelta);
            if (BorrowReturnRules.ValidateCorrection(request.Quantity, reversible, Net(ev.SourceItemRecordNumber), request.Note) is { } err) return Task.FromResult(BorrowWriteResult.Fail(err));
            var id = _next++;
            Events.Add(ev with { Id = id, EventType = BorrowReturnEventType.Correction, QuantityDelta = -request.Quantity, EventAt = request.EventAt, Actor = request.Actor, Note = request.Note, CorrectsEventId = ev.Id, CreatedAt = request.EventAt });
            if (request.ClientRequestId is not null) ClientRequests[request.ClientRequestId] = id;
            return Task.FromResult(BorrowWriteResult.Ok(id));
        }
        private long Add(BorrowSourceBill b, BorrowSourceItem i, BorrowReturnEventType t, decimal qty, DateTime at, string actor, string? note, long? corrects)
        {
            var id = _next++;
            Events.Add(new BorrowReturnEvent { Id = id, SourceItemRecordNumber = i.SourceRecordNumber, SourceBillRecordNumber = b.SourceRecordNumber, ReceiveNo = b.ReceiveNo, WorkingCode = i.WorkingCode, DrugName = i.DrugName,
                FacilityCode = b.FacilityCode, FacilityName = b.FacilityName, EventType = t, QuantityDelta = qty, EventAt = at, Actor = actor, Note = note, CorrectsEventId = corrects, CreatedAt = at });
            return id;
        }
        private BorrowReturnEvent WithReversible(BorrowReturnEvent e) => e with { ReversibleQty = e.EventType == BorrowReturnEventType.Return ? e.QuantityDelta + Events.Where(c => c.CorrectsEventId == e.Id).Sum(c => c.QuantityDelta) : 0m };
        public Task<IReadOnlyList<BorrowReturnEvent>> GetHistoryAsync(BorrowHistoryFilter f, CancellationToken cancellationToken = default)
        {
            Guard();
            IReadOnlyList<BorrowReturnEvent> r = Events.Where(e => (f.FacilityCode is null || e.FacilityCode == f.FacilityCode) && (f.ReceiveNo is null || e.ReceiveNo == f.ReceiveNo)
                && (f.Item is null || e.WorkingCode.Contains(f.Item) || (e.DrugName ?? "").Contains(f.Item, StringComparison.OrdinalIgnoreCase)) && (f.Actor is null || e.Actor.Contains(f.Actor)))
                .OrderByDescending(e => e.EventAt).ThenByDescending(e => e.Id).Select(WithReversible).ToList();
            return Task.FromResult(r);
        }
        public Task<IReadOnlyList<BorrowReturnEvent>> GetItemHistoryAsync(int itemRn, CancellationToken cancellationToken = default)
        { Guard(); return Task.FromResult<IReadOnlyList<BorrowReturnEvent>>(Events.Where(e => e.SourceItemRecordNumber == itemRn).OrderByDescending(e => e.Id).Select(WithReversible).ToList()); }
        public Task<BorrowReturnEvent?> GetEventAsync(long id, CancellationToken cancellationToken = default)
        { Guard(); var e = Events.FirstOrDefault(x => x.Id == id); return Task.FromResult(e is null ? null : WithReversible(e)); }
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
        Assert.Contains("สถานบริการ 2", text); Assert.Contains("บิล 3", text);
        Assert.Contains("จำนวนที่ยืม 755", text); Assert.Contains("คืนแล้ว 0", text); Assert.Contains("คงค้าง 755", text);
        // facility list: semantic rows (no Bootstrap table), name primary + code secondary, numbers, link to detail
        var list = Regex.Match(html, "<div[^>]*class=\"[^\"]*borrow-facility-list[^\"]*\"[^>]*>(.*?)<details", RegexOptions.Singleline);
        Assert.True(list.Success, "borrow-facility-list expected");
        Assert.DoesNotContain("<table", list.Groups[1].Value);
        Assert.DoesNotContain("table-responsive", html);
        var rows = Regex.Matches(list.Groups[1].Value, "<a[^>]*class=\"[^\"]*borrow-facility-row[^\"]*\"[^>]*href=\"(/Borrow/Facility/[^\"]+)\"");
        Assert.Equal(2, rows.Count);
        Assert.StartsWith("/Borrow/Facility/CUB001", rows[0].Groups[1].Value);   // newest facility first (filter carried in the query string)
        Assert.Contains("class=\"borrow-facility-name\"", html);
        Assert.Contains("<span class=\"borrow-facility-code\">CUB001 ·", html);
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
        Assert.Contains("placeholder=\"ค้นหาสถานบริการ เลขบิล เลขเอกสาร รหัสยา หรือชื่อยา\"", byName);
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
        Assert.Matches(@"บิล\s*2", summary); Assert.Matches(@"รายการ\s*3", summary); Assert.Matches(@"ยืม\s*750", summary); Assert.Matches(@"คงค้าง\s*750", summary);
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
        Assert.Contains("class=\"borrow-item-figures\"", html);
        Assert.Contains("เอกสาร 24082569", html);
        Assert.Contains("AMILORIDE + HCTZ (5+50MG) MODURETIC TAB", html);
        Assert.Contains("808640.", html);
        Assert.DoesNotContain("class=\"card", html);
        Assert.Contains("href=\"/Borrow\"", html);                              // breadcrumb back to the index
        // within-facility filter is real: drug/code/lot or bill number; totals in the header stay unfiltered
        var (_, filtered) = await GetAsync("/Borrow/Facility/CUB001?q=Acyclovir");
        Assert.Single(Regex.Matches(filtered, "class=\"[^\"]*borrow-item-row[^\"]*\""));
        Assert.Contains("Acyclovir 400 mg tab", filtered); Assert.DoesNotContain("AMILORIDE", filtered);
        Assert.Matches(@"รายการ\s*3", Text(Regex.Match(filtered, "<div[^>]*class=\"[^\"]*borrow-summary[^\"]*\"[^>]*>(.*?)</div>", RegexOptions.Singleline).Groups[1].Value));
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
