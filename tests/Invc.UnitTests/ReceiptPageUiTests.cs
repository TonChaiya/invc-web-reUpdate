using System.Net;
using System.Text.RegularExpressions;
using Invc.Core.Receipts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Invc.UnitTests;

/// <summary>
/// /Receipts, /Receipts/Detail/{receiveNo}, /Receipts/Print/{receiveNo} through the real Razor pipeline with a fake repository.
/// Structural assertions only (one primary type control, selected-context summary, one page scrollbar, A–Z lines).
/// </summary>
public sealed class ReceiptPageUiTests : IClassFixture<ReceiptPageUiTests.Factory>
{
    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("InvDatabase:ConnectionString", "Server=test;Initial Catalog=INV;Integrated Security=True");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<INonPoReceiptRepository>();
                services.AddSingleton<INonPoReceiptRepository>(new FakeRepo());
            });
        }
    }

    private sealed class FakeRepo : INonPoReceiptRepository
    {
        private static readonly IReadOnlyList<NonPoReceiptSummary> Headers =
        [
            new() { ReceiveNo = "O6900084", InvoiceNo = "24082569", InvoiceDate = new DateTime(2026, 8, 24), DateReceive = new DateTime(2026, 8, 24), TypeCode = "09", TypeName = "ยายืมจากหน่วยงานอื่น", SourceCode = "CUB001", SourceName = "ศูนย์สาธารณสุขและการแพทย์ตำบลทรายมูล", TotalItem = 1, TotalValue = 0m, LineCount = 1, LineQtySum = 250 },
            new() { ReceiveNo = "O6900085", InvoiceNo = "IV-5", InvoiceDate = new DateTime(2026, 9, 1), DateReceive = new DateTime(2026, 9, 2), TypeCode = "10", TypeName = "รับจาก CUP", SourceCode = "CUP01", SourceName = "โรงพยาบาลแม่ข่าย", TotalItem = 2, TotalValue = 735m, LineCount = 2, LineQtySum = 36, Note = "งวด 1" },
            new() { ReceiveNo = "O6900086", DateReceive = new DateTime(2026, 9, 5), TypeCode = "09", TypeName = "ยายืมจากหน่วยงานอื่น", SourceCode = "PAO001", SourceName = "รพ.สต.บ้านต้นเปา", TotalItem = 1, TotalValue = 0m, LineCount = 1, LineQtySum = 5 },
        ];

        public Task<IReadOnlyList<int>> GetFiscalYearsWithDataAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<int>>([2569, 2568]);
        public Task<IReadOnlyList<ReceiptTypeOption>> GetTypesAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ReceiptTypeOption>>([new("01", "ยาบริจาค"), new("05", "ยาคืนจากหน่วยเบิก"), new("09", "ยายืมจากหน่วยงานอื่น"), new("10", "รับจาก CUP")]);
        public Task<IReadOnlyList<NonPoReceiptSummary>> GetHeadersAsync(int fy, string? keyword, CancellationToken ct = default)
        {
            IReadOnlyList<NonPoReceiptSummary> r = fy != 2569 ? []
                : Headers.Where(h => keyword is null || h.ReceiveNo.Contains(keyword, StringComparison.OrdinalIgnoreCase) || (h.InvoiceNo?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) || (h.SourceName?.Contains(keyword) ?? false)).ToList();
            return Task.FromResult(r);
        }
        public Task<NonPoReceiptDetail?> GetDetailAsync(string receiveNo, CancellationToken ct = default)
        {
            if (receiveNo != "O6900085") return Task.FromResult<NonPoReceiptDetail?>(null);
            return Task.FromResult<NonPoReceiptDetail?>(new NonPoReceiptDetail
            {
                Header = Headers[1],
                Lines =
                [
                    new() { WorkingCode = "1000456", DrugName = "PARACETAMOL 500 MG TAB", QtyOrder = 24, PackRatio = 12, UnitValue = 245m, ExpiredDate = new DateTime(2020, 1, 1), LotNo = "L-OLD", Location = "ยาเม็ดทั่วไป" },
                    new() { WorkingCode = "1000123", DrugName = "AMOXICILLIN 500 MG CAP", QtyOrder = 12, PackRatio = 12, UnitValue = 245m, ExpiredDate = new DateTime(2028, 6, 30), LotNo = "L-NEW", Location = "ยาเม็ดทั่วไป" },
                ],
            });
        }
    }

    private readonly HttpClient _client;
    public ReceiptPageUiTests(Factory factory) { _client = factory.CreateClient(); }

    private async Task<(HttpStatusCode Code, string Html)> GetAsync(string url)
    {
        var r = await _client.GetAsync(url);
        return (r.StatusCode, WebUtility.HtmlDecode(await r.Content.ReadAsStringAsync()));
    }

    private static string Text(string fragment) => Regex.Replace(Regex.Replace(fragment, "<[^>]+>", " "), @"\s+", " ").Trim();

    [Fact]
    public async Task List_has_year_and_type_selects_no_permanent_type_breakdown_and_a_collapsed_summary_below_the_list()
    {
        var (code, html) = await GetAsync("/Receipts?fy=2569");
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.Contains("class=\"ds-page-title\">รับเข้าอื่น / รับจาก CUP</h1>", html);
        Assert.DoesNotContain("Non-PO Receipts", html);
        Assert.Single(Regex.Matches(html, "<select class=\"ds-select\" id=\"fy\" name=\"fy\""));
        Assert.Single(Regex.Matches(html, "<select class=\"ds-select\" id=\"type\" name=\"type\""));
        Assert.Contains("<option value=\"all\" selected=\"selected\">ทุกประเภท</option>", html);
        Assert.Contains("<option value=\"09\">09 — ยายืมจากหน่วยงานอื่น</option>", html);
        Assert.DoesNotContain("form-select", html);
        Assert.DoesNotContain("inv-cards", html);
        Assert.DoesNotContain("class=\"card", html);
        Assert.DoesNotContain("table-responsive", html);
        // the complete breakdown exists only as a collapsed disclosure AFTER the receipt list
        var details = Regex.Match(html, "<details class=\"ds-help receipts-breakdown\">\\s*<summary>([^<]*)</summary>");
        Assert.True(details.Success);
        Assert.Contains("ดูสรุปทุกประเภท", details.Groups[1].Value);
        Assert.DoesNotContain("<details open", html);
        var list = html.IndexOf("class=\"ds-list receipt-list\"", StringComparison.Ordinal);
        Assert.True(list > 0 && list < details.Index);
        Assert.DoesNotContain("ยาบริจาค", html[..list].Replace("<option value=\"01\">01 — ยาบริจาค</option>", ""));   // no category cards/chips above the list
        Assert.Contains("รวมทุกประเภท", html);
        Assert.Contains("<summary>ที่มาของข้อมูล</summary>", html);
    }

    [Fact]
    public async Task Context_summary_describes_the_filtered_rows_and_filters_compose_with_year_and_keyword()
    {
        var (_, all) = await GetAsync("/Receipts?fy=2569");
        Assert.Contains("ปีงบ 2569 · 3 ใบ · 4 รายการ · จำนวน 291 หน่วย · มูลค่า 735.00 บาท", Text(all));
        Assert.Equal(3, Regex.Matches(all, "<tr class=\"ds-row receipt-row\">").Count);

        var (_, t09) = await GetAsync("/Receipts?fy=2569&type=09");
        Assert.Contains("<option value=\"09\" selected=\"selected\">", t09);
        Assert.Contains("ปีงบ 2569 · ยายืมจากหน่วยงานอื่น · 2 ใบ · 2 รายการ · จำนวน 255 หน่วย · มูลค่า 0.00 บาท", Text(t09));
        Assert.Contains("จากทั้งหมด 3 ใบในปีนี้", Text(t09));
        Assert.Equal(2, Regex.Matches(t09, "<tr class=\"ds-row receipt-row\">").Count);
        Assert.DoesNotContain("O6900085", Regex.Match(t09, "<tbody>.*</tbody>", RegexOptions.Singleline).Value);
        // selected type is not repeated as a per-row type label
        Assert.DoesNotContain("receipt-type\"", Regex.Match(t09, "<tbody>.*</tbody>", RegexOptions.Singleline).Value);
        Assert.Contains("<th scope=\"col\" class=\"receipt-col-source\">แหล่งที่มา</th>", t09);
        // type + keyword + year
        var (_, mixed) = await GetAsync("/Receipts?fy=2569&type=09&q=ทรายมูล");
        Assert.Single(Regex.Matches(mixed, "<tr class=\"ds-row receipt-row\">"));
        Assert.Contains("ค้นหา “ทรายมูล”", mixed);
        var form = Regex.Match(mixed, "<form[^>]*class=\"ds-toolbar receipts-toolbar\"[^>]*>(.*?)</form>", RegexOptions.Singleline).Groups[1].Value;
        Assert.Contains("<option value=\"2569\" selected=\"selected\">", form);
        Assert.Contains("value=\"ทรายมูล\"", form);
        var clear = Regex.Match(mixed, "<a class=\"ds-btn ds-btn-secondary\" href=\"([^\"]+)\">ล้าง</a>");
        Assert.True(clear.Success); Assert.Contains("fy=2569", clear.Groups[1].Value); Assert.DoesNotContain("type=", clear.Groups[1].Value); Assert.DoesNotContain("q=", clear.Groups[1].Value);
        // empty state is calm text
        var (_, none) = await GetAsync("/Receipts?fy=2569&type=01");
        Assert.Contains("class=\"ds-empty\"", none); Assert.Contains("ไม่มีใบรับในเงื่อนไขนี้", none); Assert.DoesNotContain("alert-secondary", none);
    }

    [Fact]
    public async Task Rows_keep_receive_no_type_source_invoice_date_line_count_qty_and_value()
    {
        var (_, html) = await GetAsync("/Receipts?fy=2569");
        var row = Regex.Matches(html, "<tr class=\"ds-row receipt-row\">(.*?)</tr>", RegexOptions.Singleline).Cast<Match>().Single(m => m.Value.Contains("O6900085")).Groups[1].Value;
        Assert.Contains("href=\"/Receipts/Detail/O6900085\"", row);
        var t = Text(row);
        Assert.Contains("O6900085", t);
        Assert.Contains("2 ก.ย. 2569", t);
        Assert.Contains("เอกสาร IV-5", t);
        Assert.Contains("10 รับจาก CUP", t);
        Assert.Contains("โรงพยาบาลแม่ข่าย", t);
        Assert.Contains("CUP01", t);
        Assert.Contains("36 หน่วย", t);
        Assert.Contains("2 รายการ", t);
        Assert.Contains("735.00", t);
        Assert.DoesNotContain("badge", html);
    }

    [Fact]
    public async Task Detail_keeps_header_fields_summary_lines_lot_metadata_expired_text_reconciliation_and_a_to_z()
    {
        var (code, html) = await GetAsync("/Receipts/Detail/O6900085");
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.Contains("<h1 class=\"ds-page-title receipt-number\">O6900085</h1>", html);
        Assert.DoesNotContain("breadcrumb-item", html);
        Assert.DoesNotContain("inv-cards", html);
        Assert.DoesNotContain("class=\"card", html);
        Assert.DoesNotContain("table-danger", html);
        Assert.DoesNotContain("table-responsive", html);
        var t = Text(html);
        Assert.Contains("10 รับจาก CUP", t);
        Assert.Contains("โรงพยาบาลแม่ข่าย · CUP01 2 กันยายน 2569", t);
        Assert.Contains("เอกสาร IV-5 · วันที่เอกสาร 1 ก.ย. 2569 · ปีงบ 2569", t);
        var band = Text(Regex.Match(html, "<section class=\"ds-detail-summary receipt-summary\".*?</section>", RegexOptions.Singleline).Value);
        Assert.Contains("ประเภทการรับ 10 รับจาก CUP รายการ 2 จำนวน (หน่วยจ่าย) 36 มูลค่า (บาท) 735.00", band);
        foreach (var label in new[] { "เลขที่ใบรับ", "ประเภทการรับ", "แหล่งที่มา", "รหัสแหล่ง", "เลขเอกสาร", "วันที่เอกสาร", "วันที่รับ", "หมายเหตุ" }) Assert.Contains(label, t);
        Assert.Contains("หมายเหตุ งวด 1", t);
        // lines: A–Z (AMOX before PARA), every value, lot metadata grouped, expired = text chip + accent only
        var names = Regex.Matches(html, "class=\"ds-item-name\"[^>]*>([^<]+)<").Cast<Match>().Select(m => m.Groups[1].Value).ToArray();
        Assert.Equal(["AMOXICILLIN 500 MG CAP", "PARACETAMOL 500 MG TAB"], names);
        var lines = Text(Regex.Match(html, "<table class=\"ds-table receipt-lines\">.*?</table>", RegexOptions.Singleline).Value);
        Assert.Contains("1000456 · หมดอายุ Lot L-OLD · หมดอายุ 1 ม.ค. 2563 · ยาเม็ดทั่วไป · ราคา/แพ็ค 245.00 24 12 2.00 แพ็ค 245.00 490.00 Lot L-OLD หมดอายุ 1 ม.ค. 2563 ยาเม็ดทั่วไป", lines);
        Assert.Contains("class=\"ds-row receipt-line is-expired\"", html);
        Assert.Contains("receipt-expired\">หมดอายุ</span>", html);
        Assert.Single(Regex.Matches(html, "receipt-expired\">หมดอายุ</span>"));
        Assert.Contains("รวมรายการ 735.00", lines);
        Assert.Contains("หัวใบรับ (TOTAL_VALUE / TOTAL_ITEM) 735.00 2 รายการ", lines);
        Assert.Contains("status-chip-success receipt-recon\">ยอดตรงกัน</span>", html);
    }

    [Fact]
    public async Task Detail_returns_404_for_unknown_or_invalid_receipt()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await GetAsync("/Receipts/Detail/O6999999")).Code);
        Assert.Equal(HttpStatusCode.NotFound, (await GetAsync("/Receipts/Detail/bad%20no")).Code);
    }

    [Fact]
    public async Task Print_is_a_formal_document_without_the_app_shell_and_keeps_all_data()
    {
        var (code, html) = await GetAsync("/Receipts/Print/O6900085");
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.DoesNotContain("app-sidebar", html);
        Assert.Contains("class=\"print-title\">ใบรับเข้าอื่น (ไม่อ้างอิงใบสั่งซื้อ) O6900085</h1>", html);
        Assert.Contains("<dt>ประเภท</dt><dd><strong>รับจาก CUP</strong></dd>", html);
        Assert.Contains("<dt>พิมพ์เมื่อ</dt>", html);
        var t = Text(html);
        Assert.Contains("โรงพยาบาลแม่ข่าย (CUP01)", t);
        Assert.Contains("รายการที่รับ (2 รายการ) — ยอดตรงกับหัวใบรับ", t);
        Assert.Contains("class=\"print-table receipt-print-lines\"", html);
        Assert.Contains("L-OLD", t); Assert.Contains("1 ม.ค. 2563 (หมดอายุ)", t); Assert.Contains("ยาเม็ดทั่วไป", t);
        Assert.Contains("หัวใบรับ (TOTAL_VALUE / TOTAL_ITEM) 735.00 2 รายการ", t);
        Assert.DoesNotContain("badge", html);
        Assert.DoesNotContain("class=\"card", html);
        var names = Regex.Matches(html, "<td class=\"code\">(\\d+)</td>").Cast<Match>().Select(m => m.Groups[1].Value).ToArray();
        Assert.Equal(["1000123", "1000456"], names);   // A–Z on print too
    }

    [Fact]
    public void Receipts_css_has_no_inner_scroll_container()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Invc.slnx"))) dir = dir.Parent;
        var css = File.ReadAllText(Path.Combine(dir!.FullName, "src", "Invc.Web", "wwwroot", "css", "site.css"));
        var start = css.IndexOf("Receipts (รับเข้าอื่น", StringComparison.Ordinal);
        var end = css.IndexOf("Borrow (", start, StringComparison.Ordinal);
        var block = css[start..end];
        Assert.DoesNotContain("max-height", block);
        Assert.DoesNotContain("overflow: auto", block);
        Assert.DoesNotContain("overflow-y", block);
        Assert.DoesNotContain("overflow-x", block);
    }
}
