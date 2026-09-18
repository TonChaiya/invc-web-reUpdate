using System.Net;
using System.Text.RegularExpressions;
using Invc.Core.Dashboard;
using Invc.Core.Diagnostics;
using Invc.Core.Inventory;
using Invc.Core.PurchaseOrders;
using Invc.Core.Receipts;
using Invc.Core.Reorder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Invc.UnitTests;

/// <summary>
/// The dashboard (/) through the real Razor pipeline and the real <see cref="DashboardService"/> composition, with fake
/// repositories. Structural assertions for the owner's hierarchy (actions first, one fact once, pipeline = PO view).
/// </summary>
public sealed class DashboardPageUiTests : IClassFixture<DashboardPageUiTests.Factory>
{
    public sealed class Factory : WebApplicationFactory<Program>
    {
        public bool AnalyticsFail { get; set; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("InvDatabase:ConnectionString", "Server=test;Initial Catalog=INV;Integrated Security=True");
            builder.UseSetting("AppDatabase:ConnectionString", "Server=127.0.0.1;Database=invc_web_test;User ID=root;Password=;");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDatabaseHealth>();
                services.AddSingleton<IDatabaseHealth>(new FakeHealth());
                services.RemoveAll<IInventoryRepository>();
                services.AddSingleton<IInventoryRepository>(new InventoryPageUiTests.FakeInventory());
                services.RemoveAll<IReorderRepository>();
                services.AddSingleton<IReorderRepository>(new FakeReorder());
                services.RemoveAll<IPurchaseOrderRepository>();
                services.AddSingleton<IPurchaseOrderRepository>(new FakePo());
                services.RemoveAll<INonPoReceiptRepository>();
                services.AddSingleton<INonPoReceiptRepository>(new FakeReceipts());
                services.RemoveAll<IDashboardAnalyticsRepository>();
                services.AddSingleton<IDashboardAnalyticsRepository>(new FakeAnalytics(this));
            });
        }
    }

    private sealed class FakeHealth : IDatabaseHealth
    {
        public Task<DatabaseHealthResult> CheckAsync(CancellationToken ct = default)
            => Task.FromResult(new DatabaseHealthResult(true, "test", "INV", "reader", "16", TimeSpan.Zero, null));
    }

    private sealed class FakeReorder : IReorderRepository
    {
        public Task<IReadOnlyList<ReorderItem>> GetEligibleAsync(string? keyword, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ReorderItem>>(
        [
            new() { WorkingCode = "1000130", DrugName = "Clindamycin 300 mg cap", QtyOnHand = 0, MinLevel = 33, ReorderQty = 33, MaxLevel = 50 },
            new() { WorkingCode = "1000250", DrugName = "Gabapentin 300 mg cap", QtyOnHand = 200, MinLevel = 433, ReorderQty = 433, MaxLevel = 650 },
            new() { WorkingCode = "1000500", DrugName = "Yellow", QtyOnHand = 100, MinLevel = 100, ReorderQty = 150, MaxLevel = 200 },
            new() { WorkingCode = "1000010", DrugName = "Acyclovir", QtyOnHand = 100, MinLevel = 100, ReorderQty = 100, MaxLevel = 150 },
        ]);
        public Task<IReadOnlyList<ItemType>> GetItemTypesAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ItemType>>([]);
    }

    private sealed class FakePo : IPurchaseOrderRepository
    {
        public Task<IReadOnlyList<int>> GetOpenBudgetYearsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<int>>([2569]);
        public Task<IReadOnlyList<int>> GetPoFiscalYearsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<int>>([2569]);
        public Task<IReadOnlyList<DateTime>> GetBillOutDatesAsync(int fy, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DateTime>>([]);
        public Task<IReadOnlyList<PurchaseOrderSummary>> GetHeadersAsync(int fy, string? q, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<PurchaseOrderSummary>>(
        [
            new() { PoNo = "6900001", RealPo = "K6900001", Status = "1", TotalCost = 1790m, TotalItem = 1 },
            new() { PoNo = "6900002", RealPo = "K6900002", Status = "5", TotalCost = 3000m, TotalItem = 1 },
        ]);
        public Task<PurchaseOrderDetail?> GetDetailAsync(string realPo, CancellationToken ct = default) => Task.FromResult<PurchaseOrderDetail?>(null);
    }

    private sealed class FakeReceipts : INonPoReceiptRepository
    {
        public Task<IReadOnlyList<int>> GetFiscalYearsWithDataAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<int>>([2569]);
        public Task<IReadOnlyList<ReceiptTypeOption>> GetTypesAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ReceiptTypeOption>>([]);
        public Task<IReadOnlyList<NonPoReceiptSummary>> GetHeadersAsync(int fy, string? q, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<NonPoReceiptSummary>>(
        [
            new() { ReceiveNo = "O6900084", TypeCode = "09", DateReceive = new DateTime(2026, 8, 24), TotalValue = 0m, LineCount = 1, LineQtySum = 250 },
            new() { ReceiveNo = "O6900085", TypeCode = "10", DateReceive = new DateTime(2026, 9, 2), TotalValue = 735m, LineCount = 2, LineQtySum = 36 },
        ]);
        public Task<NonPoReceiptDetail?> GetDetailAsync(string receiveNo, CancellationToken ct = default) => Task.FromResult<NonPoReceiptDetail?>(null);
    }

    private sealed class FakeAnalytics(Factory f) : IDashboardAnalyticsRepository
    {
        private void MaybeFail() { if (f.AnalyticsFail) throw new InvalidOperationException("analytics down"); }
        public Task<DashboardBudget> GetBudgetAsync(int fy, CancellationToken ct = default) { MaybeFail(); return Task.FromResult(new DashboardBudget(fy, 2, 3_000_000m)); }
        public Task<DashboardSubstock> GetSubstockAsync(CancellationToken ct = default) => Task.FromResult(new DashboardSubstock(224_292.18m, 261, [new("D1", "ห้องยา OPD", 200, 200_000m), new("D2", "ห้องฉุกเฉิน", 61, 24_292.18m)]));
        public Task<DashboardStockCoverage> GetStockCoverageAsync(CancellationToken ct = default) => Task.FromResult(new DashboardStockCoverage("2026", "08", 262_000m, 100_000m));
        public Task<IReadOnlyList<DashboardEdNedRow>> GetLegacyEdNedAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DashboardEdNedRow>>([new("1", "ยาในบัญชียาหลักแห่งชาติ", "ED", 173, 90_000m), new("2", "ยานอกบัญชียาหลักแห่งชาติ", "NED", 8, 4_000m), new("3", "วัสดุการแพทย์", "MES", 86, 10_000m)]);
        public Task<IReadOnlyList<DashboardAgreementRow>> GetActiveAgreementsAsync(DateTime today, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DashboardAgreementRow>>([new(100, 40, 1, 25m)]);
        // processed months: Sep 2025 (opening source only), Oct 2025, Nov 2025 — Dec onward not processed
        public Task<IReadOnlyList<DashboardProcessedSnapshotRow>> GetProcessedSnapshotsAsync(int fy, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DashboardProcessedSnapshotRow>>(
        [
            new(2025, 9, "1", "ยาในบัญชียาหลักแห่งชาติ", 200, 100_000, 70_000m), new(2025, 9, "3", "วัสดุการแพทย์", 80, 8_000, 20_000m),
            new(2025, 10, "1", "ยาในบัญชียาหลักแห่งชาติ", 200, 101_000, 73_000m), new(2025, 10, "2", "ยานอกบัญชียาหลักแห่งชาติ", 5, 500, 1_500m), new(2025, 10, "3", "วัสดุการแพทย์", 80, 8_100, 21_000m),
            new(2025, 11, "1", "ยาในบัญชียาหลักแห่งชาติ", 200, 99_000, 69_000m), new(2025, 11, "2", "ยานอกบัญชียาหลักแห่งชาติ", 5, 400, 1_200m), new(2025, 11, "3", "วัสดุการแพทย์", 80, 8_000, 20_500m),
        ]);
        public Task<IReadOnlyList<DashboardProcessedFlowRow>> GetProcessedFlowsAsync(int fy, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DashboardProcessedFlowRow>>(
        [
            new(2025, 10, "1", "ยาในบัญชียาหลักแห่งชาติ", 20, 5_000, 12_000m, 4_000, 9_000m), new(2025, 10, "2", "ยานอกบัญชียาหลักแห่งชาติ", 2, 500, 1_500m, 0, 0m), new(2025, 10, "3", "วัสดุการแพทย์", 5, 300, 3_000m, 200, 2_000m),
            new(2025, 11, "1", "ยาในบัญชียาหลักแห่งชาติ", 15, 1_000, 2_000m, 3_000, 6_000m), new(2025, 11, "2", "ยานอกบัญชียาหลักแห่งชาติ", 1, 0, 0m, 100, 300m), new(2025, 11, "3", "วัสดุการแพทย์", 3, 0, 0m, 100, 500m),
        ]);
        public Task<IReadOnlyList<DashboardProcessTimeRow>> GetProcessTimeAsync(int fy, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DashboardProcessTimeRow>>([]);
        public Task<DashboardItemTrend?> GetItemTrendAsync(string code, int months, CancellationToken ct = default)
            => Task.FromResult<DashboardItemTrend?>(code == "1000123" ? new DashboardItemTrend("1000123", "AMOXICILLIN 500 MG CAP", 12_500, "แคปซูล", [new("256808", 500, 1_000m), new("256809", 250, 500m)]) : null);
    }

    private readonly Factory _factory;
    private readonly HttpClient _client;
    public DashboardPageUiTests(Factory factory) { _factory = factory; _client = factory.CreateClient(); }

    private async Task<(HttpStatusCode Code, string Html)> GetAsync(string url)
    {
        var r = await _client.GetAsync(url);
        return (r.StatusCode, WebUtility.HtmlDecode(await r.Content.ReadAsStringAsync()));
    }

    private static string Text(string fragment) => Regex.Replace(Regex.Replace(fragment, "<[^>]+>", " "), @"\s+", " ").Trim();

    [Fact]
    public async Task No_page_hero_actions_are_one_compact_panel_with_one_click_target_per_row()
    {
        _factory.AnalyticsFail = false;
        var (code, html) = await GetAsync("/?fy=2569");
        Assert.Equal(HttpStatusCode.OK, code);
        // no page hero: the shell already names the page; the h1 is only for assistive tech
        Assert.DoesNotContain("ds-page-title", html);
        Assert.DoesNotContain("ds-page-header", html);
        Assert.DoesNotContain("สถานะคลังเวชภัณฑ์และงานที่ต้องดำเนินการ", html);
        Assert.DoesNotContain("INV อ่านอย่างเดียว", html);
        Assert.DoesNotContain("ฐานข้อมูลปกติ", html);
        Assert.Contains("<h1 class=\"visually-hidden\">ภาพรวม</h1>", html);
        Assert.Contains("class=\"dashboard-utility\"", html);
        Assert.DoesNotContain("INVC Dashboard", html);
        Assert.DoesNotContain("metric-card", Regex.Replace(html, "<!--.*?-->", ""));
        Assert.DoesNotContain("class=\"card", html);
        Assert.DoesNotContain("table-responsive", html);
        Assert.Single(Regex.Matches(html, "<select class=\"ds-select ds-select-sm\" id=\"fy\" name=\"fy\""));

        var actions = Regex.Match(html, "<section class=\"dashboard-section dashboard-actions\".*?</section>", RegexOptions.Singleline);
        Assert.True(actions.Success);
        var move = html.IndexOf("dashboard-section dashboard-movement", StringComparison.Ordinal);
        var stock = html.IndexOf("dashboard-section dashboard-stock", StringComparison.Ordinal);
        Assert.True(actions.Index < move && move < stock, "actions → monthly movement → stock overview");
        Assert.Contains("<h2 id=\"h-actions\" class=\"dashboard-section-title\">งานที่ต้องดำเนินการ</h2>", actions.Value);
        Assert.Single(Regex.Matches(actions.Value, "class=\"dashboard-action-panel\""));
        Assert.DoesNotContain("dashboard-action-open", actions.Value);
        Assert.DoesNotContain("เปิด</", actions.Value);
        var rows = Regex.Matches(actions.Value, "<a class=\"dashboard-action-row ([^\"]+)\"[^>]*href=\"([^\"]+)\"[^>]*>(.*?)</a>", RegexOptions.Singleline);
        Assert.True(rows.Count >= 2);
        // one click target per concept: each href appears exactly once in the panel
        Assert.Single(Regex.Matches(actions.Value, "href=\"/Reorder\\?status=red\""));
        Assert.Single(Regex.Matches(actions.Value, "href=\"/PurchaseOrders\\?fy=2569&bucket=issued\""));
        Assert.Equal("is-danger", rows[0].Groups[1].Value);
        Assert.Contains("/Reorder?status=red", rows[0].Groups[2].Value);
        Assert.Equal("ต้องสั่งซื้อทันที 2 รายการ แนะนำสั่ง 500 หน่วย ›", Text(rows[0].Groups[3].Value));
        Assert.Equal("is-warning", rows[1].Groups[1].Value);
        Assert.Contains("ใกล้ถึงจุดสั่งซื้อ 1 รายการ", Text(rows[1].Groups[3].Value));
        var poRow = rows.Cast<Match>().Single(m => m.Groups[2].Value.Contains("/PurchaseOrders"));
        Assert.Equal("is-workflow", poRow.Groups[1].Value);
        Assert.Equal("ใบสั่งซื้อที่ยังดำเนินการ 1 ใบ 1,790.00 บาท · เสร็จสิ้นแล้ว 1 ใบ ›", Text(poRow.Groups[3].Value));   // issued 2 − closed 1
    }

    [Fact]
    public async Task Inventory_value_appears_once_with_edned_coverage_and_substock_as_supporting_panels()
    {
        _factory.AnalyticsFail = false;
        var (_, html) = await GetAsync("/?fy=2569");
        Assert.Single(Regex.Matches(html, "1,245,782\\.50"));                       // main-store value exactly once
        var stock = Text(Regex.Match(html, "<section class=\"dashboard-section dashboard-stock\".*?</section>", RegexOptions.Singleline).Value);
        Assert.Contains("3 รายการใช้งาน 92,530 หน่วย มูลค่า 1,245,782.50 บาท", stock);
        Assert.Contains("ED 173 (65%)", stock); Assert.Contains("NED 8 (3%)", stock); Assert.Contains("MES 86 (32%)", stock);
        Assert.Contains("composition-bar", html);
        Assert.Contains("อัตราสำรองคลัง 2.62 เดือน งวด ส.ค. 2569", stock);
        Assert.Contains("ข้อมูลสรุปสิ้นเดือน ไม่ใช่ยอดคงคลังปัจจุบัน", stock);
        Assert.Contains("คลังย่อย 224,292.18 บาท 2 หน่วยเบิก · 261 รายการ ห้องยา OPD 200,000.00", stock);
        Assert.DoesNotContain("มูลค่าคลังหลัก", html);
    }

    [Fact]
    public async Task Pipeline_is_the_primary_po_view_with_budget_agreements_process_time_and_receipts_as_support()
    {
        _factory.AnalyticsFail = false;
        var (_, html) = await GetAsync("/?fy=2569");
        var proc = Regex.Match(html, "<section class=\"dashboard-section dashboard-procurement\".*?</section>", RegexOptions.Singleline).Value;
        var t = Text(proc);
        Assert.Contains("งบประมาณ 3,000,000.00 บาท · 2 รายการงบ", t);
        var steps = Regex.Matches(proc, "<li class=\"dashboard-pipeline-step[^\"]*\">\\s*<a[^>]*href=\"([^\"]+)\"[^>]*>(.*?)</a>", RegexOptions.Singleline);
        Assert.Equal(5, steps.Count);
        Assert.Equal(["ออกใบสั่งซื้อ 2 4,790.00", "ตรวจรับ 1 3,000.00", "ตั้งหนี้ 1 3,000.00", "การเงิน 1 3,000.00", "เสร็จสิ้น 1 3,000.00"], steps.Cast<Match>().Select(m => Text(m.Groups[2].Value)).ToArray());
        Assert.All(steps.Cast<Match>(), m => Assert.Contains("/PurchaseOrders?fy=2569&bucket=", m.Groups[1].Value));
        // no separate top-level PO KPI competing with the pipeline
        Assert.DoesNotContain("ใบสั่งซื้อ ปีงบ", html);
        Assert.Contains("สัญญาจะซื้อจะขายคงเหลือ 1,500.00 บาท 1 สัญญาที่ยังไม่ครบ/ไม่หมดอายุ", t);   // (100−40)×1×25
        Assert.Contains("ระยะเวลากระบวนการ PO (วัน) ยังไม่มีข้อมูล", t);
        Assert.Contains("รับเข้าอื่น / CUP 2 ใบ 3 รายการ · 735.00 บาท ดูรายการ →", t);
        Assert.DoesNotContain("รับเข้าอื่น / CUP ปีงบ", html);
    }

    [Fact]
    public async Task Movement_is_the_main_focus_reads_as_an_equation_and_expands_per_month_from_the_month_cell()
    {
        _factory.AnalyticsFail = false;
        var (_, html) = await GetAsync("/?fy=2569&item=1000123");
        var move = Regex.Match(html, "<section class=\"dashboard-section dashboard-movement\".*?</section>", RegexOptions.Singleline).Value;
        Assert.Contains("class=\"ds-table dashboard-movement-table\"", move);
        Assert.Contains("ความเคลื่อนไหวรายเดือน <span class=\"dashboard-section-note\">มูลค่า (บาท) · เดือนที่ประมวลผลแล้ว", move);
        // no technical paragraph under the title; the source explanation stays behind the disclosure
        Assert.DoesNotContain("dashboard-movement-note", move);
        Assert.DoesNotContain("ผลประมวลผลรายเดือนจาก INVC", move);
        Assert.Contains("<summary>ที่มาของข้อมูล</summary>", move);
        var mt = Text(move);
        // the header itself is the equation
        Assert.Contains("เดือน ยอดยกมา + รับเข้า − จ่ายออก = คงเหลือ", mt);
        Assert.DoesNotContain("คงเหลือปลายเดือน", mt);
        // only processed months (Oct, Nov 2025), newest first; Sep 2025 is opening source only, Dec+ not shown as zero months
        var rows = Regex.Matches(move, "<tr class=\"ds-row dashboard-processed-row([^\"]*)\">");
        Assert.Equal(2, rows.Count);
        Assert.Equal(" is-latest", rows[0].Groups[1].Value); Assert.Equal("", rows[1].Groups[1].Value);
        Assert.DoesNotContain("ธ.ค. 2568", mt); Assert.DoesNotContain("ล่าสุด", mt); Assert.DoesNotContain("ดูแยกหมวดเวชภัณฑ์", mt);
        Assert.DoesNotContain("2025-10", mt); Assert.DoesNotContain("2025-11", mt);   // no technical YYYY-MM keys
        Assert.Contains("พ.ย. 2568 — แยกหมวดเวชภัณฑ์ 95,500.00 2,000.00 6,800.00 90,700.00", mt);   // Oct ending 95,500 → Nov opening
        Assert.Contains("ต.ค. 2568 — แยกหมวดเวชภัณฑ์ 90,000.00 16,500.00 11,000.00 95,500.00", mt); // Sep 2025 ending → Oct opening
        Assert.True(mt.IndexOf("พ.ย. 2568", StringComparison.Ordinal) < mt.IndexOf("ต.ค. 2568", StringComparison.Ordinal), "newest month first");
        // the month cell is the single expand control: button with aria-expanded/aria-controls → hidden row directly below
        Assert.Equal(2, Regex.Matches(move, "<button type=\"button\" class=\"dashboard-month-toggle\" aria-expanded=\"false\" aria-controls=\"mt-panel-2568(10|11)\">").Count);
        Assert.Contains("<tr class=\"dashboard-type-row\" id=\"mt-panel-256810\" hidden>", move);
        Assert.DoesNotContain("dashboard-type-toggle", move); Assert.DoesNotContain("type=\"checkbox\"", move);
        Assert.Contains("js/dashboard-movement.", html);
        // expander: ยา รวม + ED + NED always, other types when present, grand total = month row, no diff line when exact
        var oct = Regex.Match(move, "<tr class=\"dashboard-type-row\" id=\"mt-panel-256810\" hidden>.*?</div>", RegexOptions.Singleline).Value;
        var ot = Text(oct);
        Assert.Contains("ประเภท ยอดยกมา รับเข้า จ่ายออก คงเหลือ", ot);
        Assert.Contains("ยา รวม (ED + NED) 70,000.00 13,500.00 9,000.00 74,500.00", ot);
        Assert.Contains("ยาในบัญชียาหลักแห่งชาติ (ED) 70,000.00 12,000.00 9,000.00 73,000.00", ot);
        Assert.Contains("ยานอกบัญชียาหลักแห่งชาติ (NED) 0.00 1,500.00 0.00 1,500.00", ot);
        Assert.Contains("วัสดุการแพทย์ (3) 20,000.00 3,000.00 2,000.00 21,000.00", ot);
        Assert.Contains("รวมทั้งหมด 90,000.00 16,500.00 11,000.00 95,500.00", ot);
        Assert.DoesNotContain("ยาตัวอย่าง", ot); Assert.DoesNotContain("ไม่ระบุประเภท", ot);
        Assert.DoesNotContain("ส่วนต่างจากผลประมวลผล", oct);
        Assert.DoesNotContain("dashboard-type-warn", move);
        Assert.DoesNotContain("RO / RS / SS / SO", move);                                   // CARD categories gone from this section
        Assert.Contains("<summary>ที่มาของข้อมูล</summary>", move);
        Assert.DoesNotContain("dashboard-panel", move.Replace("dashboard-type-panel", ""));
        Assert.DoesNotContain("<script", move);

        var trend = Regex.Match(html, "<section class=\"dashboard-section dashboard-trend\".*?</section>", RegexOptions.Singleline);
        Assert.True(trend.Index > move.Length, "item trend comes after movement");
        Assert.Contains("name=\"item\"", trend.Value);
        Assert.Contains("AMOXICILLIN 500 MG CAP", trend.Value);
        Assert.Contains("ส.ค. 2568 500 1,000.00", Text(trend.Value));
        var (_, noItem) = await GetAsync("/?fy=2569");
        Assert.Contains("class=\"dashboard-quiet\">เลือกรหัสยาเพื่อดูประวัติการจ่ายรายเดือน</p>", noItem);
        var (_, unknown) = await GetAsync("/?fy=2569&item=9999999");
        Assert.Contains("ไม่พบรหัสยา “9999999” ในคลัง", unknown);
    }

    [Fact]
    public async Task Failed_sections_render_small_localized_warnings_and_an_action_row()
    {
        _factory.AnalyticsFail = true;
        try
        {
            var (code, html) = await GetAsync("/?fy=2569");
            Assert.Equal(HttpStatusCode.OK, code);
            Assert.Contains("class=\"dashboard-unavailable\"", html);
            Assert.Contains("<strong>งบประมาณ</strong> — ส่วนนี้อ่านข้อมูลไม่สำเร็จ", html);
            Assert.Contains("ส่วนที่แสดงข้อมูลไม่ได้", html);
            Assert.Contains("1 ส่วนแสดงไม่ได้", html);
            Assert.DoesNotContain("alert-danger", html);
            Assert.Contains("ต้องสั่งซื้อทันที", html);                            // the rest of the dashboard still renders
        }
        finally { _factory.AnalyticsFail = false; }
    }

    [Fact]
    public void Dashboard_css_has_no_inner_scroll_container_and_no_legacy_metric_cards()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Invc.slnx"))) dir = dir.Parent;
        var css = File.ReadAllText(Path.Combine(dir!.FullName, "src", "Invc.Web", "wwwroot", "css", "site.css"));
        var start = css.IndexOf("Dashboard (ภาพรวม)", StringComparison.Ordinal);
        var end = css.IndexOf("Borrow (", start, StringComparison.Ordinal);
        var block = css[start..end];
        Assert.DoesNotContain("max-height", block);
        Assert.DoesNotContain("overflow: auto", block);
        Assert.DoesNotContain("overflow-y", block);
        Assert.DoesNotContain("overflow-x", block);
        Assert.DoesNotContain("overflow: scroll", block);
        Assert.DoesNotContain(".metric-card {", css, StringComparison.Ordinal);
        Assert.DoesNotContain(".pipeline-step {", css, StringComparison.Ordinal);
    }
}
