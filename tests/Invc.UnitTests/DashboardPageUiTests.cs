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
        public Task<IReadOnlyList<DashboardMovementRow>> GetMovementAsync(int fy, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DashboardMovementRow>>(
        [
            new("256810", "R", "O", 3, 12_000m, 300), new("256810", "S", "S", 10, 9_000m, 900),
            new("256811", "R", "O", 1, 4_000m, 100), new("256811", "S", "O", 4, 2_500m, 250),
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
    public async Task Actions_come_first_with_urgent_reorder_on_top_and_no_kpi_card_wall()
    {
        _factory.AnalyticsFail = false;
        var (code, html) = await GetAsync("/?fy=2569");
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.Contains("class=\"ds-page-title\">ภาพรวม</h1>", html);
        Assert.DoesNotContain("INVC Dashboard", html);
        Assert.DoesNotContain("metric-card", Regex.Replace(html, "<!--.*?-->", ""));
        Assert.DoesNotContain("class=\"card", html);
        Assert.DoesNotContain("table-responsive", html);
        Assert.Single(Regex.Matches(html, "<select class=\"ds-select\" id=\"fy\" name=\"fy\""));

        var actions = Regex.Match(html, "<section class=\"dashboard-section dashboard-actions\".*?</section>", RegexOptions.Singleline);
        Assert.True(actions.Success);
        var stock = html.IndexOf("dashboard-section dashboard-stock", StringComparison.Ordinal);
        Assert.True(actions.Index < stock, "actions must precede the stock overview");
        var rows = Regex.Matches(actions.Value, "<a class=\"dashboard-action-row ([^\"]+)\"[^>]*href=\"([^\"]+)\"[^>]*>(.*?)</a>", RegexOptions.Singleline);
        Assert.True(rows.Count >= 2);
        Assert.Equal("is-danger", rows[0].Groups[1].Value);
        Assert.Contains("/Reorder?status=red", rows[0].Groups[2].Value);
        Assert.Contains("ต้องสั่งซื้อทันที 2 รายการ · แนะนำสั่งรวม 500 หน่วย 2 เปิด", Text(rows[0].Groups[3].Value));
        Assert.Equal("is-warning", rows[1].Groups[1].Value);
        Assert.Contains("ใกล้ถึงจุดสั่งซื้อ 1 รายการ", Text(rows[1].Groups[3].Value));
        var poRow = rows.Cast<Match>().Single(m => m.Groups[2].Value.Contains("/PurchaseOrders"));
        Assert.Equal("is-workflow", poRow.Groups[1].Value);
        Assert.Contains("ใบสั่งซื้อที่ยังดำเนินการ 1 ใบ · 1,790.00 บาท · เสร็จสิ้นแล้ว 1 ใบ", Text(poRow.Groups[3].Value));   // issued 2 − closed 1
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
    public async Task Movement_and_item_trend_are_secondary_sections_with_shared_tables_and_no_chart_library()
    {
        _factory.AnalyticsFail = false;
        var (_, html) = await GetAsync("/?fy=2569&item=1000123");
        var move = Regex.Match(html, "<section class=\"dashboard-section dashboard-movement\".*?</section>", RegexOptions.Singleline).Value;
        Assert.Contains("class=\"ds-table dashboard-movement-table\"", move);
        var mt = Text(move);
        Assert.Contains("ต.ค. 2568 12,000.00 9,000.00 13", mt);
        Assert.Contains("รวม 16,000.00 11,500.00 18", mt);
        Assert.Contains("<summary>รายละเอียดประเภท</summary>", move);          // legacy codes only behind disclosure
        Assert.DoesNotContain("<th scope=\"col\">ประเภท (รหัสเดิม)</th>", move);
        Assert.Contains("dashboard-bar dashboard-bar--in", move);
        Assert.DoesNotContain("chart.js", html, StringComparison.OrdinalIgnoreCase);

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
        Assert.DoesNotContain(".metric-card {", css, StringComparison.Ordinal);
        Assert.DoesNotContain(".pipeline-step {", css, StringComparison.Ordinal);
    }
}
