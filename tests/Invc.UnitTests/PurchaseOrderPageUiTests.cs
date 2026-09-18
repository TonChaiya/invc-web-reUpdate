using System.Net;
using System.Text.RegularExpressions;
using Invc.Core.PurchaseOrders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Invc.UnitTests;

/// <summary>
/// /PurchaseOrders, /PurchaseOrders/Detail/{realPo} and /PurchaseOrders/Print/{realPo} through the real Razor pipeline with a fake
/// repository. Structural assertions only (owner UX rules: one primary control, one page scrollbar, A–Z lines); no pixel/colour checks.
/// </summary>
public sealed class PurchaseOrderPageUiTests : IClassFixture<PurchaseOrderPageUiTests.Factory>
{
    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("InvDatabase:ConnectionString", "Server=test;Initial Catalog=INV;Integrated Security=True");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPurchaseOrderRepository>();
                services.AddSingleton<IPurchaseOrderRepository>(new FakeRepo());
            });
        }
    }

    private sealed class FakeRepo : IPurchaseOrderRepository
    {
        // FY2569: one issued (status 1, nothing received), one received (status 2, first receive), one closed (status 5, every date).
        private static readonly IReadOnlyList<PurchaseOrderSummary> Headers =
        [
            new() { PoNo = "6900001", RealPo = "K6900001", DocNo = "DOC-1", PoDate = new DateTime(2026, 9, 15), Status = "1", StatusName = "ออกใบสั่งซื้อ", VendorCode = "V1", VendorName = "บริษัท เอ จำกัด", BudgetType = "B1", BudgetName = "เงินบำรุง", BuyMethod = "M1", BuyMethodName = "เฉพาะเจาะจง", EdNed = "1", EdNedName = "ยาในบัญชียาหลักแห่งชาติ", TotalItem = 2, TotalCost = 1790m },
            new() { PoNo = "6900002", RealPo = "K6900002", DocNo = "DOC-2", PoDate = new DateTime(2026, 9, 10), Status = "2", StatusName = "ตรวจรับแล้ว", VendorCode = "V2", VendorName = "บริษัท บี จำกัด", BudgetType = "B1", BudgetName = "เงินบำรุง", BuyMethod = "M1", BuyMethodName = "เฉพาะเจาะจง", TotalItem = 1, TotalCost = 500m, FirstReceiveDate = new DateTime(2026, 9, 12) },
            new() { PoNo = "6900003", RealPo = "K6900003", DocNo = "DOC-3", PoDate = new DateTime(2026, 8, 1), Status = "5", StatusName = "เสร็จสิ้น", VendorCode = "V1", VendorName = "บริษัท เอ จำกัด", BudgetType = "B2", BudgetName = "งบลงทุน", BuyMethod = "M2", BuyMethodName = "ประกวดราคา", TotalItem = 1, TotalCost = 3000m, FirstReceiveDate = new DateTime(2026, 8, 5), BillOutAcc = new DateTime(2026, 8, 10), BillOutFin = new DateTime(2026, 8, 15), BillEndAcc = new DateTime(2026, 8, 20), BillEnd = new DateTime(2026, 8, 21), BillIn = new DateTime(2026, 8, 2), BillOut = new DateTime(2026, 8, 3), BillPayFin = new DateTime(2026, 8, 18), ApproveNoFin = "AP-9" },
        ];

        public Task<IReadOnlyList<int>> GetOpenBudgetYearsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<int>>([2569]);
        public Task<IReadOnlyList<int>> GetPoFiscalYearsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<int>>([2569, 2568]);
        public Task<IReadOnlyList<DateTime>> GetBillOutDatesAsync(int fiscalYear, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DateTime>>([]);

        public Task<IReadOnlyList<PurchaseOrderSummary>> GetHeadersAsync(int fiscalYear, string? keyword, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<PurchaseOrderSummary> r = fiscalYear != 2569 ? []
                : Headers.Where(h => keyword is null || h.DisplayNumber.Contains(keyword, StringComparison.OrdinalIgnoreCase) || (h.VendorName?.Contains(keyword) ?? false) || (h.DocNo?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();
            return Task.FromResult(r);
        }

        public Task<PurchaseOrderDetail?> GetDetailAsync(string realPo, CancellationToken cancellationToken = default)
        {
            if (realPo != "K6900001") return Task.FromResult<PurchaseOrderDetail?>(null);
            return Task.FromResult<PurchaseOrderDetail?>(new PurchaseOrderDetail
            {
                Header = Headers[0],
                Lines =
                [
                    new() { RunNo = 1, WorkingCode = "1000456", DrugName = "PARACETAMOL 500 MG TAB", DrugPo = "PARA 500", QtyOrder = 1000, PackRatio1 = 500, PoUnit = "BOX", BuyUnitCost = 450m, BuyValue = 900m, QtyFree = 100, PackRatio2 = 500, Note = "ส่งด่วน" },
                    new() { RunNo = 2, WorkingCode = "1000123", DrugName = "AMOXICILLIN 500 MG CAP", QtyOrder = 200, PackRatio1 = 100, PoUnit = "BOX", BuyUnitCost = 445m, BuyValue = 890m },
                ],
                Receipts =
                [
                    new() { ReceiveNo = "R6900010", InvoiceNo = "INV-77", InvoiceDate = new DateTime(2026, 9, 17), DateReceive = new DateTime(2026, 9, 18), TotalCost = 890m, TotalItem = 1, DateAcc = null,
                            Lines = [ new() { WorkingCode = "1000123", DrugName = "AMOXICILLIN 500 MG CAP", QtyOrder = 200, PackRatio1 = 100, BuyUnitCost = 445m, QtyFree = 0, ExpiredDate1 = new DateTime(2028, 1, 31), Location1 = "A01", LotNo = "LOT-9" } ] },
                ],
            });
        }
    }

    private readonly HttpClient _client;
    public PurchaseOrderPageUiTests(Factory factory) { _client = factory.CreateClient(); }

    private async Task<(HttpStatusCode Code, string Html)> GetAsync(string url)
    {
        var r = await _client.GetAsync(url);
        return (r.StatusCode, WebUtility.HtmlDecode(await r.Content.ReadAsStringAsync()));
    }

    private static string Text(string fragment) => Regex.Replace(Regex.Replace(fragment, "<[^>]+>", " "), @"\s+", " ").Trim();

    [Fact]
    public async Task List_has_fiscal_year_select_search_and_one_workflow_selector_with_six_buckets_and_no_card_wall()
    {
        var (code, html) = await GetAsync("/PurchaseOrders?fy=2569&bucket=received");
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.Contains("class=\"ds-page-title\">ใบสั่งซื้อ</h1>", html);
        Assert.DoesNotContain("Purchase Orders</span>", html);
        Assert.DoesNotContain("OTH_IVO", Regex.Match(html, "<div class=\"ds-page-header.*?</div>\\s*</div>", RegexOptions.Singleline).Value);

        // fiscal year select + search in one shared toolbar
        Assert.Single(Regex.Matches(html, "<select class=\"ds-select\" id=\"fy\" name=\"fy\""));
        Assert.Contains("<option value=\"2569\" selected=\"selected\">ปีงบ 2569</option>", html);
        Assert.Contains("<option value=\"2568\">ปีงบ 2568</option>", html);
        var form = Regex.Match(html, "<form[^>]*class=\"ds-toolbar po-toolbar\"[^>]*>(.*?)</form>", RegexOptions.Singleline);
        Assert.True(form.Success);
        Assert.Contains("name=\"bucket\" value=\"received\"", form.Groups[1].Value);
        Assert.Contains("class=\"ds-input\"", form.Groups[1].Value);
        Assert.DoesNotContain("form-control", html);
        Assert.DoesNotContain("form-select", html);

        // six buckets in ONE segmented control, current one marked; no Bootstrap card wall
        var nav = Regex.Match(html, "<nav class=\"po-tabs\"[^>]*>(.*?)</nav>", RegexOptions.Singleline);
        Assert.True(nav.Success);
        var tabs = Regex.Matches(nav.Groups[1].Value, "<a class=\"po-tab[^\"]*\"[^>]*>(.*?)</a>", RegexOptions.Singleline);
        Assert.Equal(6, tabs.Count);
        Assert.Equal(["ออกใบสั่งซื้อ 3", "ตรวจรับ 2", "ตั้งหนี้ 1", "การเงิน 1", "เสร็จสิ้น 1", "ทั้งหมด 3"], tabs.Cast<Match>().Select(m => Text(m.Groups[1].Value)).ToArray());
        var current = tabs.Cast<Match>().Where(m => m.Value.Contains("aria-current=\"page\"")).ToList();
        Assert.Single(current);
        Assert.Contains("bucket=received", current[0].Value);
        Assert.Contains("is-active", current[0].Value);
        Assert.DoesNotContain("inv-cards", html);
        Assert.DoesNotContain("class=\"card", html);
        Assert.DoesNotContain("reorder-card-active", html);
        Assert.DoesNotContain("table-responsive", html);

        // context line
        Assert.Contains("ปีงบ 2569 · ตรวจรับแล้ว · 2 ใบ · มูลค่า 3,500.00 บาท", Text(html));
    }

    [Fact]
    public async Task Search_and_tabs_preserve_fiscal_year_and_bucket_and_clear_keeps_both()
    {
        var (_, html) = await GetAsync("/PurchaseOrders?fy=2569&bucket=closed&q=DOC-3");
        var links = Regex.Matches(html, "<a class=\"po-tab[^\"]*\"[^>]*href=\"([^\"]+)\"");
        Assert.Equal(6, links.Count);
        Assert.All(links.Cast<Match>(), m => { Assert.Contains("fy=2569", m.Groups[1].Value); Assert.Contains("q=DOC-3", m.Groups[1].Value); });
        var clear = Regex.Match(html, "<a class=\"ds-btn ds-btn-secondary\" href=\"([^\"]+)\">ล้าง</a>");
        Assert.True(clear.Success);
        Assert.Contains("fy=2569", clear.Groups[1].Value); Assert.Contains("bucket=closed", clear.Groups[1].Value); Assert.DoesNotContain("q=", clear.Groups[1].Value);
        Assert.Contains("ค้นหา “DOC-3”", html);
        Assert.Single(Regex.Matches(html, "<tr class=\"ds-row po-row\">"));
    }

    [Fact]
    public async Task Rows_keep_every_operational_value_and_progress_is_a_step_list_without_repeating_the_bucket_label()
    {
        var (_, issued) = await GetAsync("/PurchaseOrders?fy=2569&bucket=issued");
        var rows = Regex.Matches(issued, "<tr class=\"ds-row po-row\">(.*?)</tr>", RegexOptions.Singleline);
        Assert.Equal(3, rows.Count);
        var closed = rows.Cast<Match>().Single(m => m.Value.Contains("K6900003")).Groups[1].Value;
        var t = Text(closed);
        Assert.Contains("href=\"/PurchaseOrders/Detail/K6900003\"", closed);
        Assert.Contains("บริษัท เอ จำกัด", t);
        Assert.Contains("เอกสาร DOC-3", t);
        Assert.Contains("1 ส.ค. 2569", t);
        Assert.Contains("3,000.00", t);
        Assert.Contains("1 รายการ", t);
        Assert.Contains("งบลงทุน", t);           // budget
        Assert.Contains("ประกวดราคา", t);        // buy method
        // progress steps from existing dates only
        Assert.Contains("● รับของ 5 ส.ค. 69", t);
        Assert.Contains("● ตั้งหนี้ 10 ส.ค. 69", t);
        Assert.Contains("● การเงิน 15 ส.ค. 69", t);
        Assert.Contains("● ปิดบัญชี 20 ส.ค. 69", t);
        var open = Text(rows.Cast<Match>().Single(m => m.Value.Contains("K6900001")).Groups[1].Value);
        Assert.Contains("○ รับของ", open);
        Assert.Contains("รอรับของ", open);
        // a specific bucket does not repeat the bucket label / status chip on every row
        Assert.DoesNotContain("po-status-chip", issued);
        Assert.DoesNotContain("ออกใบสั่งซื้อแล้ว", Regex.Match(issued, "<tbody>.*</tbody>", RegexOptions.Singleline).Value);

        // bucket = all → raw status chip per row
        var (_, all) = await GetAsync("/PurchaseOrders?fy=2569&bucket=all");
        Assert.Equal(3, Regex.Matches(all, "class=\"po-status-chip").Count);
        Assert.Contains("5 เสร็จสิ้น", Text(all));
    }

    [Fact]
    public async Task Status_breakdown_and_help_stay_collapsed_below_the_list()
    {
        var (_, html) = await GetAsync("/PurchaseOrders?fy=2569");
        var breakdown = Regex.Match(html, "<details class=\"ds-help po-breakdown\">");
        Assert.True(breakdown.Success);
        Assert.DoesNotContain("<details open", html);
        Assert.True(html.IndexOf("class=\"ds-list po-list\"", StringComparison.Ordinal) < breakdown.Index);
        Assert.Contains("แยกตามรหัสสถานะ (TblPOStatus)", html);
        Assert.Contains("<summary>เกณฑ์ขั้นตอน</summary>", html);
        Assert.Contains("OTH_IVO", html);          // technical distinction lives in the help, not the header
    }

    [Fact]
    public async Task Detail_exposes_identity_summary_every_document_date_timeline_lines_reconciliation_and_receipts()
    {
        var (code, html) = await GetAsync("/PurchaseOrders/Detail/K6900001");
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.Contains("<h1 class=\"ds-page-title po-number\">K6900001</h1>", html);
        Assert.DoesNotContain("breadcrumb-item", html);
        Assert.DoesNotContain("inv-cards", html);
        Assert.DoesNotContain("class=\"card", html);
        Assert.DoesNotContain("table-responsive", html);
        var t = Text(html);
        Assert.Contains("บริษัท เอ จำกัด 15 กันยายน 2569", t);
        Assert.Contains("เลขเอกสาร DOC-1 · เฉพาะเจาะจง · เงินบำรุง · ยาในบัญชียาหลักแห่งชาติ", t);
        // summary band
        var band = Text(Regex.Match(html, "<section class=\"ds-detail-summary po-detail-summary\".*?</section>", RegexOptions.Singleline).Value);
        Assert.Contains("มูลค่าใบสั่งซื้อ (บาท) 1,790.00", band);
        Assert.Contains("จำนวนรายการ 2", band);
        Assert.Contains("สถานะ 1 ออกใบสั่งซื้อ", band);
        Assert.Contains("ความคืบหน้า รอรับของ", band);
        // every source/document date still rendered
        foreach (var label in new[] { "เอกสารเข้า (BILLIN)", "เอกสารออก (BILLOUT)", "ส่งตั้งหนี้", "ส่งเอกสารการเงิน", "เลขที่ขออนุมัติ", "ตัดจ่าย", "ปิดบัญชี", "สิ้นสุด (BILLEND)", "ปีงบประมาณ", "วันที่สั่งซื้อ" })
            Assert.Contains(label, t);
        // timeline from existing dates only: สั่งซื้อ done, the rest pending
        var timeline = Regex.Match(html, "<ol class=\"po-timeline\".*?</ol>", RegexOptions.Singleline).Value;
        Assert.Equal(5, Regex.Matches(timeline, "<li").Count);
        Assert.Single(Regex.Matches(timeline, "<li class=\"is-done\">"));
        Assert.Contains("● สั่งซื้อ 15 ก.ย. 2569", Text(timeline));
        Assert.Contains("○ รับของ –", Text(timeline));
        // lines: A–Z (AMOX before PARA although RunNo says otherwise), all business values, reconciliation
        var names = Regex.Matches(html, "class=\"ds-item-name\"[^>]*>([^<]+)<").Cast<Match>().Select(m => m.Groups[1].Value).ToArray();
        Assert.Equal(["AMOXICILLIN 500 MG CAP", "PARACETAMOL 500 MG TAB"], names);
        var lines = Text(Regex.Match(html, "<table class=\"ds-table po-lines\">.*?</table>", RegexOptions.Singleline).Value);
        Assert.Contains("1000456 · หน่วยสั่ง BOX · ชื่อใน PO: PARA 500", lines);
        Assert.Contains("แถม 100 (0.20 แพ็ค)", lines);
        Assert.Contains("ส่งด่วน", lines);
        Assert.Contains("1,000 500 2.00 แพ็ค 450.00 900.00", lines);
        Assert.Contains("รวมรายการ (SUM BUY_VALUE) 1,790.00", lines);
        Assert.Contains("หัวใบสั่งซื้อ (TOTAL_COST / TOTAL_ITEM) 1,790.00 2 รายการ", lines);
        Assert.Contains("status-chip-success po-recon\">ยอดตรงกัน</span>", html);
        // receipts as document sections
        Assert.Contains("การรับของ", t);
        Assert.Single(Regex.Matches(html, "<section class=\"po-receipt\""));
        var rc = Text(Regex.Match(html, "<section class=\"po-receipt\".*?</section>", RegexOptions.Singleline).Value);
        Assert.Contains("ใบรับ R6900010 890.00 บาท", rc);
        Assert.Contains("รับของ 18 ก.ย. 2569 · ใบแจ้งหนี้ INV-77 ลงวันที่ 17 ก.ย. 2569 · 1 รายการ", rc);
        Assert.Contains("AMOXICILLIN 500 MG CAP 1000123 · Lot LOT-9 · หมดอายุ 31 ม.ค. 2571 · A01 · ราคา 445.00 200 2.00 แพ็ค × 100", rc);
        Assert.DoesNotContain("<table", Regex.Match(html, "<section class=\"po-receipts\".*", RegexOptions.Singleline).Value);
    }

    [Fact]
    public async Task Detail_returns_404_for_unknown_or_invalid_po()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await GetAsync("/PurchaseOrders/Detail/K6999999")).Code);
        Assert.Equal(HttpStatusCode.NotFound, (await GetAsync("/PurchaseOrders/Detail/bad%20po")).Code);
    }

    [Fact]
    public async Task Print_is_a_formal_document_without_the_app_shell_and_keeps_all_data()
    {
        var (code, html) = await GetAsync("/PurchaseOrders/Print/K6900001");
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.DoesNotContain("app-sidebar", html);
        Assert.DoesNotContain("po-tabs", html);
        Assert.Contains("class=\"print-title\">ใบสั่งซื้อ K6900001</h1>", html);
        Assert.Contains("<dt>บริษัท</dt><dd><strong>บริษัท เอ จำกัด</strong></dd>", html);
        var t = Text(html);
        Assert.Contains("ข้อมูลใบสั่งซื้อ", t); Assert.Contains("ขั้นตอนเอกสาร", t);
        Assert.Contains("สิ้นสุด (BILLEND) –", t);
        Assert.Contains("รายการสั่งซื้อ (2 รายการ) — ยอดตรงกับหัวใบสั่งซื้อ", t);
        Assert.Contains("class=\"print-table po-print-lines\"", html);
        Assert.Contains("class=\"print-table po-print-receipt\"", html);
        Assert.Contains("ใบรับ R6900010", t);
        Assert.Contains("LOT-9", t);
        Assert.DoesNotContain("badge", html);
        Assert.DoesNotContain("class=\"card", html);
    }

    /// <summary>INVC with no purchase orders at all (empty MS_PO, no open BUDGET year): the pages must still open.</summary>
    public sealed class EmptyFactory : WebApplicationFactory<Program>
    {
        private sealed class EmptyRepo : IPurchaseOrderRepository
        {
            public Task<IReadOnlyList<int>> GetOpenBudgetYearsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<int>>([]);
            public Task<IReadOnlyList<int>> GetPoFiscalYearsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<int>>([]);
            public Task<IReadOnlyList<DateTime>> GetBillOutDatesAsync(int fy, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DateTime>>([]);
            public Task<IReadOnlyList<PurchaseOrderSummary>> GetHeadersAsync(int fy, string? q, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<PurchaseOrderSummary>>([]);
            public Task<PurchaseOrderDetail?> GetDetailAsync(string realPo, CancellationToken ct = default) => Task.FromResult<PurchaseOrderDetail?>(null);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("InvDatabase:ConnectionString", "Server=test;Initial Catalog=INV;Integrated Security=True");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPurchaseOrderRepository>();
                services.AddSingleton<IPurchaseOrderRepository>(new EmptyRepo());
            });
        }
    }

    [Fact]
    public async Task List_opens_normally_when_invc_has_no_purchase_orders_at_all()
    {
        using var factory = new EmptyFactory();
        using var client = factory.CreateClient();
        var r = await client.GetAsync("/PurchaseOrders");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var html = WebUtility.HtmlDecode(await r.Content.ReadAsStringAsync());
        var text = Text(html);
        var fy = Invc.Core.Common.ThaiFiscalYear.FromDate(DateTime.Today);
        Assert.Contains($"ปีงบ {fy}", text);                                     // falls back to the current Thai fiscal year
        Assert.Contains("ยังไม่มีใบสั่งซื้อ — ใช้ปีงบประมาณปัจจุบัน", text);      // explained, not an error
        Assert.Equal(6, Regex.Matches(html, "<a class=\"po-tab").Count);         // selector still there, all counts 0
        Assert.Contains($"ไม่มีใบสั่งซื้อในปีงบประมาณ {fy}", text);
        Assert.DoesNotContain("alert-danger", html);
        Assert.DoesNotContain("Exception", html);
        // detail / print of a PO that does not exist → clean 404, not a crash
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/PurchaseOrders/Detail/K6900001")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/PurchaseOrders/Print/K6900001")).StatusCode);
    }

    [Fact]
    public void Po_css_has_no_inner_scroll_container()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Invc.slnx"))) dir = dir.Parent;
        var css = File.ReadAllText(Path.Combine(dir!.FullName, "src", "Invc.Web", "wwwroot", "css", "site.css"));
        var start = css.IndexOf("Purchase Orders (ใบสั่งซื้อ)", StringComparison.Ordinal);
        var end = css.IndexOf("Borrow (", start, StringComparison.Ordinal);
        var block = css[start..end];
        Assert.DoesNotContain("max-height", block);
        Assert.DoesNotContain("overflow: auto", block);
        Assert.DoesNotContain("overflow-y", block);
        Assert.DoesNotContain("overflow-x", block);
    }
}
