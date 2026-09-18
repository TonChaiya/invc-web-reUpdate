using System.Net;
using System.Text.RegularExpressions;
using Invc.Core.Inventory;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Invc.UnitTests;

/// <summary>
/// /Inventory/Status and /Inventory/Detail/{code} rendered through the real Razor pipeline with a fake repository.
/// Structural assertions only (owner UX rule "one concept = one primary control"); no pixel/colour assertions.
/// </summary>
public sealed class InventoryPageUiTests : IClassFixture<InventoryPageUiTests.Factory>
{
    public sealed class Factory : WebApplicationFactory<Program>
    {
        public FakeInventory Inventory { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("InvDatabase:ConnectionString", "Server=test;Initial Catalog=INV;Integrated Security=True");
            builder.UseSetting("AppDatabase:ConnectionString", "Server=127.0.0.1;Database=invc_web_test;User ID=root;Password=;");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IInventoryRepository>();
                services.AddSingleton<IInventoryRepository>(Inventory);
            });
        }
    }

    public sealed class FakeInventory : IInventoryRepository
    {
        public IReadOnlyList<InventoryItem> Items { get; } =
        [
            new() { WorkingCode = "1000123", DrugName = "AMOXICILLIN 500 MG CAP", HospCode = "AMX500", Ven = "E", QtyOnHand = 12_500, SaleUnit = "แคปซูล", Location = "A01", RatePerMonth = 2_500, BorrowableQty = 6_250 },
            new() { WorkingCode = "1000456", DrugName = "PARACETAMOL 500 MG TAB", Ven = "E", QtyOnHand = 80_000, SaleUnit = "เม็ด", Location = "A02", RatePerMonth = null, BorrowableQty = 0 },
            new() { WorkingCode = "3000860", DrugName = "ซอง sterile 6 นิ้ว", Ven = "N", QtyOnHand = 30, SaleUnit = "ซอง", Location = "S1", RatePerMonth = 10, BorrowableQty = 15 },
        ];

        public Task<IReadOnlyList<InventoryItem>> GetStatusAsync(string? keyword, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<InventoryItem> r = keyword is null ? Items
                : Items.Where(i => i.DrugName.Contains(keyword, StringComparison.OrdinalIgnoreCase) || i.WorkingCode.Contains(keyword)).ToList();
            return Task.FromResult(r);
        }

        public Task<InventorySummary> GetSummaryAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(InventorySummary.FromGroups(
            [
                new("1", "ยาในบัญชียาหลักแห่งชาติ", "ED", 2, 92_500, 1_245_632.50m),
                new("3", "วัสดุการแพทย์", "วัสดุ", 1, 30, 150m),
            ]));

        public Task<InventoryItemDetail?> GetDetailAsync(string workingCode, CancellationToken cancellationToken = default)
        {
            if (workingCode != "1000123") return Task.FromResult<InventoryItemDetail?>(null);
            return Task.FromResult<InventoryItemDetail?>(new InventoryItemDetail
            {
                WorkingCode = "1000123", DrugName = "AMOXICILLIN 500 MG CAP", Composition = "Amoxicillin 500 mg", DosageForm = "CAP",
                SaleUnit = "แคปซูล", Location = "A01", HospCode = "AMX500", Ven = "E", Abc = "A", EdNedName = "ยาในบัญชียาหลักแห่งชาติ",
                QtyOnHand = 12_500, TotalValue = 18_750.25m, RatePerMonth = 2_500, BorrowableQty = 6_250,
                Lots =
                [
                    new() { PackRatio = 500, QtyOnHand = 10_000, ExpiredDate = new DateTime(2027, 12, 31), LotNo = "LOT-A", Location = "A01", LotValue = 15_000m, VendorName = "บริษัท ก", ManufacName = "ผู้ผลิต ก", TradeName = "AMOXY-A" },
                    new() { PackRatio = 100, QtyOnHand = 2_550, ExpiredDate = new DateTime(2020, 1, 1), LotNo = "LOT-OLD", Location = "A01", LotValue = 3_750.25m, VendorCode = "V2", ManufacCode = "M2", TradeName = "AMOXY-B" },
                ],
            });
        }

        public int StatusLotsCalls;
        public Task<IReadOnlyList<InventoryLot>> GetStatusLotsAsync(string? keyword, CancellationToken cancellationToken = default)
        {
            StatusLotsCalls++;
            var detail = GetDetailAsync("1000123").Result!;
            IReadOnlyList<InventoryLot> lots = keyword is not null && !"AMOXICILLIN 500 MG CAP".Contains(keyword, StringComparison.OrdinalIgnoreCase) && !"1000123".Contains(keyword)
                ? []
                : detail.Lots.Select(l => l with { WorkingCode = "1000123" }).ToList();
            return Task.FromResult(lots);
        }

        public Task<IReadOnlyList<ItemLotTotals>> GetLotTotalsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ItemLotTotals>>([]);
    }

    private readonly HttpClient _client;
    public InventoryPageUiTests(Factory factory) { _client = factory.CreateClient(); }

    private async Task<(HttpStatusCode Code, string Html)> GetAsync(string url)
    {
        var r = await _client.GetAsync(url);
        return (r.StatusCode, WebUtility.HtmlDecode(await r.Content.ReadAsStringAsync()));
    }

    private static string Text(string fragment) => Regex.Replace(Regex.Replace(fragment, "<[^>]+>", " "), @"\s+", " ").Trim();

    [Fact]
    public async Task Status_page_has_one_primary_search_a_compact_summary_strip_and_the_list_as_main_area()
    {
        var (code, html) = await GetAsync("/Inventory/Status");
        Assert.Equal(HttpStatusCode.OK, code);

        // 1. primary search control
        Assert.Contains("class=\"ds-toolbar inventory-toolbar\"", html);
        Assert.Contains("name=\"q\"", html);
        Assert.Contains("class=\"ds-input\"", html);

        // 4. no Bootstrap summary-card wall: one inline strip with the three totals
        Assert.DoesNotContain("inv-cards", html);
        Assert.DoesNotContain("class=\"card", html);
        var strip = Regex.Match(html, "<div[^>]*class=\"ds-summary inventory-summary\"[^>]*>(.*?)</div>", RegexOptions.Singleline);
        Assert.True(strip.Success, "summary strip expected");
        var stripText = Text(strip.Groups[1].Value);
        Assert.Contains("3 รายการ", stripText);
        Assert.Contains("92,530 หน่วย", stripText);
        Assert.Contains("มูลค่า 1,245,782.50 บาท", stripText);

        // result context line
        Assert.Contains("แสดง 3 รายการ", Text(html));

        // 2./3. complete category breakdown is NOT permanent: it sits in a collapsed <details> AFTER the list
        var details = Regex.Match(html, "<details[^>]*class=\"[^\"]*inventory-breakdown[^\"]*\"[^>]*>\\s*<summary>([^<]*)</summary>", RegexOptions.Singleline);
        Assert.True(details.Success, "collapsed breakdown expected");
        Assert.Contains("ดูสรุปทุกหมวด", details.Groups[1].Value);
        Assert.DoesNotContain("<details open", html);
        Assert.True(html.IndexOf("class=\"inventory-list", StringComparison.Ordinal) < details.Index, "breakdown must come after the primary list");
        Assert.Contains("ยาในบัญชียาหลักแห่งชาติ (ED)", html);
        Assert.Contains("วัสดุการแพทย์ (วัสดุ)", html);
    }

    [Fact]
    public async Task Status_list_keeps_all_operational_data_and_links_to_detail()
    {
        var (_, html) = await GetAsync("/Inventory/Status");
        var rows = Regex.Matches(html, "<tr class=\"inventory-row\">(.*?)</tr>", RegexOptions.Singleline);
        Assert.Equal(3, rows.Count);
        var first = rows[0].Groups[1].Value;
        Assert.Contains("href=\"/Inventory/Detail/1000123\"", first);
        Assert.Contains("class=\"inventory-item-name\"", first);
        var t = Text(first);
        // name · code (hosp) · VEN · unit · location · stock · months · rate · borrowable
        Assert.Contains("AMOXICILLIN 500 MG CAP", t);
        Assert.Contains("1000123 (AMX500)", t);
        Assert.Contains("VEN E", t);
        Assert.Contains("แคปซูล", t);
        Assert.Contains("A01", t);
        Assert.Contains("12,500", t);
        Assert.Contains(" 5 ", t + " ");            // 12,500 / 2,500 = 5 months (InventoryRules unchanged)
        Assert.Contains("2,500", t);
        Assert.Contains("6,250", t);
        // N/A coverage stays visible as text
        Assert.Contains("N/A", Text(rows[1].Groups[1].Value));
        // search still works through the same page
        var (_, filtered) = await GetAsync("/Inventory/Status?q=sterile");
        Assert.Single(Regex.Matches(filtered, "<tr class=\"inventory-row\">"));
        Assert.Contains("ค้นหา “sterile”", filtered);
        Assert.Contains("href=\"/Inventory/Status\"", filtered);   // ล้าง
    }

    [Fact]
    public async Task Detail_page_exposes_core_values_grouped_sections_and_lot_table()
    {
        var (code, html) = await GetAsync("/Inventory/Detail/1000123");
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.Contains("<h1 class=\"ds-page-title\">AMOXICILLIN 500 MG CAP</h1>", html);
        Assert.Contains("กลับสถานะคงคลัง", html);
        Assert.DoesNotContain("inv-cards", html);
        Assert.DoesNotContain("class=\"card", html);

        var band = Regex.Match(html, "<section class=\"inventory-detail-summary\"[^>]*>(.*?)</section>", RegexOptions.Singleline);
        Assert.True(band.Success);
        var b = Text(band.Groups[1].Value);
        Assert.Contains("คงคลัง (แคปซูล) 12,500", b);
        Assert.Contains("เหลือใช้ได้ (เดือน) 5", b);
        Assert.Contains("อัตราใช้/เดือน 2,500", b);
        Assert.Contains("ยืมได้ 6,250", b);
        Assert.Contains("มูลค่าคงคลัง (บาท) 18,750.25", b);

        var all = Text(html);
        Assert.Contains("บัญชียา ยาในบัญชียาหลักแห่งชาติ", all);
        Assert.Contains("VEN / ABC E / A", all);
        Assert.Contains("ส่วนประกอบ Amoxicillin 500 mg", all);
        Assert.Contains("ยอดล็อตไม่ตรงกับยอดคลัง", all);      // lots 10,000 + 2,550 = 12,550 ≠ header 12,500 (deliberate mismatch fixture)
        Assert.Equal(2, Regex.Matches(html, "<tr class=\"inventory-row[^\"]*\">").Count);
        Assert.Contains("inventory-row--expired", html);
        Assert.Contains(">หมดอายุ</span>", html);                // status text, not colour alone
        Assert.Contains("LOT-OLD", html);
        Assert.Contains("รวมล็อต", all);
    }

    [Fact]
    public async Task Status_rows_have_an_accessible_lot_toggle_and_a_hidden_panel_row_without_preloaded_lots()
    {
        var (_, html) = await GetAsync("/Inventory/Status");
        var buttons = Regex.Matches(html, "<button type=\"button\" class=\"inventory-lot-toggle\" data-working-code=\"(\\w+)\"\\s+aria-expanded=\"false\" aria-controls=\"lots-(\\w+)\">");
        Assert.Equal(3, buttons.Count);
        Assert.All(buttons.Cast<Match>(), m => Assert.Equal(m.Groups[1].Value, m.Groups[2].Value));
        Assert.Contains("ดูล็อต", html);
        Assert.Equal(3, Regex.Matches(html, "<tr class=\"inventory-lot-panel-row\" id=\"lots-\\w+\" hidden>").Count);
        Assert.Contains("data-lot-slot=\"1000123\" data-loaded=\"false\"></td>", html);   // empty slot: lots are not preloaded
        Assert.DoesNotContain("inventory-lot-grid", html);
        Assert.Matches(@"/js/inventory-lots(\.[a-z0-9]+)?\.js", html);   // fingerprinted static asset
    }

    [Fact]
    public async Task Lots_handler_returns_the_lot_panel_for_one_medicine_with_per_lot_pack_expressions_and_total_check()
    {
        var (code, html) = await GetAsync("/Inventory/Status?handler=Lots&workingCode=1000123");
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.DoesNotContain("<html", html);                       // partial only
        Assert.Contains("class=\"inventory-lot-panel\" data-working-code=\"1000123\"", html);
        Assert.Equal(2, Regex.Matches(html, "class=\"inventory-lot-row").Count);
        var text = Text(html);
        // column order: ชื่อการค้า → Lot → หมดอายุ → จำนวน → ขนาดบรรจุ → รวมของล็อต
        Assert.Matches("ชื่อการค้า Lot วันหมดอายุ จำนวน .*ขนาดบรรจุ รวมของล็อต", text);
        Assert.Contains("AMOXY-A Lot LOT-A", text);
        Assert.Contains("20 × 500", text);                          // 10,000 / 500
        Assert.Contains("25 × 100 + 50", text);                     // 2,550 / 100 — each lot keeps its own ratio
        Assert.Contains("รวม 2,550 แคปซูล", text);
        Assert.Contains("รวมทุกล็อต 12,550 แคปซูล", text);
        Assert.Contains("ยอดรวมล็อตไม่ตรงกับยอดคลัง (ยอดคลัง 12,500 แคปซูล)", text);
        // expiry attention: expired lot = strong class + text label; far-future lot = no chip
        Assert.Contains("inventory-lot-row is-expired", html);
        Assert.Contains(">หมดอายุ</span>", html);
        Assert.Single(Regex.Matches(html, "inventory-exp-chip"));
        Assert.DoesNotContain("บริษัท ก", html);                    // no vendor/manufacturer in the quick view
        Assert.Contains("href=\"/Inventory/Detail/1000123\"", html);
    }

    [Fact]
    public async Task AllLots_handler_returns_one_block_per_listed_item_in_a_single_lot_query()
    {
        var (code, html) = await GetAsync("/Inventory/Status?handler=AllLots");
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.DoesNotContain("<html", html);
        Assert.Equal(3, Regex.Matches(html, "<div data-lots-for=\"\\w+\">").Count);   // every listed item, lots or not
        Assert.Contains("data-lots-for=\"1000123\"", html);
        Assert.Contains("รวมทุกล็อต 12,550 แคปซูล", Text(html));
        Assert.Contains("ไม่มีข้อมูลล็อตสำหรับรายการนี้", html);                       // items without lots get an empty panel
        // keyword narrows both the items and the lots
        var (_, filtered) = await GetAsync("/Inventory/Status?handler=AllLots&q=sterile");
        Assert.Single(Regex.Matches(filtered, "<div data-lots-for=\"\\w+\">"));
        Assert.Contains("data-lots-for=\"3000860\"", filtered);
    }

    [Fact]
    public async Task Status_page_has_a_show_all_lots_button_that_is_hidden_without_javascript()
    {
        var (_, html) = await GetAsync("/Inventory/Status?q=amox");
        Assert.Contains("id=\"inventory-lots-all\"", html);
        Assert.Matches("id=\"inventory-lots-all\"\\s+data-q=\"amox\" aria-pressed=\"false\" hidden>", html);
        Assert.Contains("แสดงล็อตทั้งหมด", html);
    }

    [Fact]
    public async Task Lots_handler_rejects_invalid_or_unknown_codes()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await GetAsync("/Inventory/Status?handler=Lots&workingCode=1000123x!")).Code);
        Assert.Equal(HttpStatusCode.NotFound, (await GetAsync("/Inventory/Status?handler=Lots&workingCode=")).Code);
        Assert.Equal(HttpStatusCode.NotFound, (await GetAsync("/Inventory/Status?handler=Lots&workingCode=9999999")).Code);
    }

    [Fact]
    public async Task Detail_returns_404_for_unknown_or_invalid_code()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await GetAsync("/Inventory/Detail/9999999")).Code);
        Assert.Equal(HttpStatusCode.NotFound, (await GetAsync("/Inventory/Detail/abc")).Code);
    }

    [Fact]
    public void Inventory_css_has_no_nested_vertical_scroll_container()
    {
        var css = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "Invc.Web", "wwwroot", "css", "site.css"));
        var start = css.IndexOf("Inventory (สถานะคงคลัง", StringComparison.Ordinal);
        Assert.True(start > 0, "inventory css block expected");
        var block = css[start..];
        Assert.DoesNotContain("max-height: calc(100vh", block);
        Assert.DoesNotContain("overflow: auto", block);
        Assert.DoesNotContain("overflow-y", block);
        // the legacy wrappers used by other modules no longer cap height either
        Assert.DoesNotContain(".inv-table-wrap { max-height: calc", css);
        Assert.DoesNotContain(".reorder-table-wrap { max-height: calc", css);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Invc.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
    }
}
