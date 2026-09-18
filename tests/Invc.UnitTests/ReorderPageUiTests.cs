using System.Net;
using System.Text.RegularExpressions;
using Invc.Core.Inventory;
using Invc.Core.Reorder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Invc.UnitTests;

/// <summary>
/// Renders /Reorder through the real Razor pipeline (WebApplicationFactory) with an in-memory
/// <see cref="IReorderRepository"/> — no database. Verifies the approved compact, table-first behaviour:
/// no summary-card grid, one status strip with counts and aria-current, search/print preserving state.
/// </summary>
public sealed class ReorderPageUiTests : IClassFixture<ReorderPageUiTests.Factory>
{
    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");   // no AllowedHosts requirement; connection string is shape-validated only
            builder.UseSetting("InvDatabase:ConnectionString", "Server=test;Initial Catalog=INV;Integrated Security=True");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IReorderRepository>();
                services.AddSingleton<IReorderRepository>(new FakeReorderRepository());
            });
        }
    }

    private sealed class FakeReorderRepository : IReorderRepository
    {
        // 2 red (stock < MIN), 1 yellow (MIN ≤ stock < ROP), 3 green, one of them without thresholds.
        private static readonly IReadOnlyList<ReorderItem> Items =
        [
            new() { WorkingCode = "1000130", DrugName = "Clindamycin 300 mg cap", QtyOnHand = 0, MinLevel = 33, ReorderQty = 33, MaxLevel = 50, SaleUnit = "CAP", RatePerMonth = 10, EdNedCode = "1", EdNedName = "ยาในบัญชียาหลักแห่งชาติ" },
            new() { WorkingCode = "1000250", DrugName = "Gabapentin 300 mg cap", QtyOnHand = 200, MinLevel = 433, ReorderQty = 433, MaxLevel = 650, SaleUnit = "CAP", RatePerMonth = 433, EdNedCode = "2", EdNedName = "ยานอกบัญชียาหลักแห่งชาติ" },
            new() { WorkingCode = "1000500", DrugName = "Yellow test item", QtyOnHand = 100, MinLevel = 100, ReorderQty = 150, MaxLevel = 200, SaleUnit = "TAB", RatePerMonth = 50, EdNedCode = "1", EdNedName = "ยาในบัญชียาหลักแห่งชาติ" },
            new() { WorkingCode = "1000010", DrugName = "Acyclovir 400 mg tab", QtyOnHand = 100, MinLevel = 100, ReorderQty = 100, MaxLevel = 150, SaleUnit = "TAB", RatePerMonth = 100, EdNedCode = "1", EdNedName = "ยาในบัญชียาหลักแห่งชาติ" },
            new() { WorkingCode = "1000020", DrugName = "Albendazole 200 mg tab", QtyOnHand = 100, MinLevel = 0, ReorderQty = 0, MaxLevel = 0, SaleUnit = "TAB", RatePerMonth = 0, EdNedCode = "1", EdNedName = "ยาในบัญชียาหลักแห่งชาติ" },
            new() { WorkingCode = "3000860", DrugName = "ซอง sterile 6 นิ้ว", QtyOnHand = 0, MinLevel = 0, ReorderQty = 0, MaxLevel = 0, SaleUnit = "ซอง", RatePerMonth = null, EdNedCode = "3", EdNedName = "วัสดุการแพทย์" },
        ];

        public Task<IReadOnlyList<ReorderItem>> GetEligibleAsync(string? keyword, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ReorderItem> result = keyword is null
                ? Items
                : Items.Where(i => i.DrugName.Contains(keyword, StringComparison.OrdinalIgnoreCase) || i.WorkingCode.Contains(keyword)).ToList();
            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<ItemType>> GetItemTypesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ItemType>>(
            [
                new("1", "ยาในบัญชียาหลักแห่งชาติ", "ED"), new("2", "ยานอกบัญชียาหลักแห่งชาติ", "NED"), new("3", "วัสดุการแพทย์", "MES"),
                new("4", "วัสดุเภสัชกรรม", "EA"), new("5", "ยาตัวอย่างเพื่อทดลองใช้", "SAM"),
            ]);
    }

    private readonly HttpClient _client;

    public ReorderPageUiTests(Factory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    private async Task<string> GetAsync(string url)
    {
        var response = await _client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Legacy_card_grid_pills_and_bootstrap_table_are_gone()
    {
        var html = await GetAsync("/Reorder");
        Assert.DoesNotContain("inv-cards", html);
        Assert.DoesNotContain("reorder-card-active", html);
        Assert.DoesNotContain("reorder-status-option", html);
        Assert.DoesNotContain("class=\"table ", html);
        Assert.DoesNotContain("table-responsive", html);
        Assert.DoesNotContain("form-control", html);
        Assert.Contains("class=\"ds-page-title\">คำแนะนำการสั่งซื้อ</h1>", html);
        Assert.DoesNotContain("Reorder Recommendations", html);
    }

    [Fact]
    public async Task Status_tabs_list_every_status_with_its_count_and_mark_the_current_one()
    {
        var html = await GetAsync("/Reorder?status=green");
        var nav = Regex.Match(html, "<nav[^>]*class=\"reorder-tabs\"[^>]*>(.*?)</nav>", RegexOptions.Singleline);
        Assert.True(nav.Success, "one <nav class=\"reorder-tabs\"> expected");
        var tabs = Regex.Matches(nav.Groups[1].Value, "<a[^>]*class=\"reorder-tab[^\"]*\"[^>]*>(.*?)</a>", RegexOptions.Singleline);
        Assert.Equal(4, tabs.Count);

        string Text(Match m) => Regex.Replace(Regex.Replace(m.Groups[1].Value, "<[^>]+>", " "), @"\s+", " ").Trim();
        Assert.Equal("ต้องสั่งซื้อทันที 2", Text(tabs[0]));
        Assert.Equal("ใกล้ถึงจุดสั่งซื้อ 1", Text(tabs[1]));
        Assert.Equal("มีสำรอง 3", Text(tabs[2]));
        Assert.Equal("ทั้งหมด 6", Text(tabs[3]));

        // accessible current state on the selected tab only (aria-current + is-active, never colour alone)
        var current = tabs.Cast<Match>().Where(m => m.Value.Contains("aria-current=\"page\"")).ToList();
        Assert.Single(current);
        Assert.Contains("status=green", current[0].Value);
        Assert.Contains("is-active", current[0].Value);
        // no second selector for the same statuses (the only <select> is ประเภทเวชภัณฑ์)
        Assert.DoesNotContain("name=\"status\"", Regex.Replace(html, "<input[^>]*>", ""));
        Assert.Single(Regex.Matches(html, "<select"));
    }

    [Fact]
    public async Task Specific_status_hides_the_per_row_status_column_and_all_shows_it()
    {
        var red = await GetAsync("/Reorder?status=red");
        Assert.DoesNotContain("reorder-col-status", red);
        Assert.DoesNotContain("reorder-status-chip", red);
        Assert.Contains("ต้องสั่งซื้อทันที</strong> · 2 รายการ", Regex.Replace(red, @"\s+", " "));
        Assert.Contains("แนะนำสั่งรวม", red);

        var all = await GetAsync("/Reorder?status=all");
        Assert.Contains("<th scope=\"col\" class=\"reorder-col-status\">สถานะ</th>", all);
        Assert.Equal(6, Regex.Matches(all, "<td class=\"reorder-col-status\">").Count);
        Assert.Contains("reorder-status-chip is-red", all);
        Assert.Contains("reorder-status-chip is-green", all);
        Assert.DoesNotContain("แนะนำสั่งรวม", all);
    }

    [Fact]
    public async Task Switching_status_preserves_the_search_keyword_and_search_preserves_the_status()
    {
        var html = await GetAsync("/Reorder?status=all&q=cap");
        var links = Regex.Matches(html, "<a[^>]*class=\"reorder-tab[^\"]*\"[^>]*href=\"([^\"]+)\"");
        Assert.Equal(4, links.Count);
        Assert.All(links.Cast<Match>(), m => Assert.Contains("q=cap", m.Groups[1].Value));
        var form = Regex.Match(html, "<form[^>]*class=\"ds-toolbar reorder-toolbar\"[^>]*>(.*?)</form>", RegexOptions.Singleline);
        Assert.True(form.Success, "one <form class=\"ds-toolbar reorder-toolbar\"> expected");
        Assert.Contains("name=\"status\" value=\"all\"", form.Groups[1].Value.Replace("' ", "\" ").Replace("'", "\""));
        Assert.Contains("class=\"ds-input\"", form.Groups[1].Value);
        Assert.Contains("placeholder=\"ค้นหารหัสยา / ชื่อยา / ส่วนประกอบ\"", form.Groups[1].Value);
        Assert.Contains("ล้าง", form.Groups[1].Value);
    }

    [Fact]
    public async Task Print_link_preserves_status_and_search_and_print_page_is_a_formal_report()
    {
        var html = await GetAsync("/Reorder?status=green&q=tab");
        var print = Regex.Match(html, "<a[^>]*href=\"(/Reorder/Print[^\"]*)\"[^>]*>");
        Assert.True(print.Success, "print link expected");
        Assert.Contains("status=green", print.Groups[1].Value);
        Assert.Contains("q=tab", print.Groups[1].Value);

        var report = await GetAsync("/Reorder/Print?status=green&q=tab");
        Assert.DoesNotContain("app-sidebar", report);
        Assert.Contains("class=\"print-title\">รายงานคำแนะนำการสั่งซื้อ</h1>", report);
        Assert.Contains("<dt>สถานะ</dt><dd><strong>มีสำรอง</strong></dd>", report);
        Assert.Contains("<dt>ค้นหา</dt><dd>“tab”</dd>", report);
        Assert.Contains("<dt>พิมพ์เมื่อ</dt>", report);
        Assert.Contains("class=\"print-table reorder-print-table\"", report);
        Assert.Contains("<th scope=\"col\" class=\"num\">MIN</th>", report);
        Assert.Contains("<th scope=\"col\" class=\"num\">จุดสั่งซื้อ</th>", report);
        Assert.Contains("<th scope=\"col\" class=\"num\">MAX</th>", report);
        Assert.Contains("มีสำรอง</td>", report);                    // status as text, readable without colour
        Assert.DoesNotContain("badge", report);
    }

    [Fact]
    public async Task Result_table_keeps_thresholds_suggested_quantity_and_a_to_z_order()
    {
        var html = await GetAsync("/Reorder?status=all");
        Assert.Contains("class=\"ds-table reorder-table\"", html);
        Assert.Contains("class=\"ds-list reorder-list\"", html);
        // ระดับคลัง cell keeps every threshold value
        Assert.Contains("<dt>MIN</dt><dd>33</dd>", html);
        Assert.Contains("<dt>จุดสั่งซื้อ</dt><dd>33</dd>", html);
        Assert.Contains("<dt>MAX</dt><dd>50</dd>", html);
        Assert.Contains("<dt>จุดสั่งซื้อ</dt><dd>150</dd>", html);      // yellow item ROP ≠ MIN
        // suggested quantity: red rows strong, green non-positive shown as text
        Assert.Contains("reorder-suggested reorder-suggested--attention\">50</span>", html);
        Assert.Contains("reorder-suggested--none", html);
        Assert.Contains("ไม่ต้องสั่ง", html);
        // owner rule: A–Z by name (not WORKING_CODE order)
        var names = Regex.Matches(html, "class=\"ds-item-name\"[^>]*>([^<]+)<").Cast<Match>().Select(m => m.Groups[1].Value).ToArray();
        Assert.Equal(["Acyclovir 400 mg tab", "Albendazole 200 mg tab", "Clindamycin 300 mg cap", "Gabapentin 300 mg cap", "Yellow test item", "ซอง sterile 6 นิ้ว"], names);
        Assert.Contains("href=\"/Inventory/Detail/1000130\"", html);
    }

    [Fact]
    public async Task Item_type_dropdown_is_the_single_category_control_and_composes_with_status_and_keyword()
    {
        var html = await GetAsync("/Reorder?status=all");
        Assert.Single(Regex.Matches(html, "<select class=\"ds-select\" id=\"type\" name=\"type\""));
        Assert.Contains("<option value=\"\">ทุกประเภท</option>", html);
        Assert.Contains("<option value=\"1\">1 — ยาในบัญชียาหลักแห่งชาติ</option>", html);
        Assert.Contains("<option value=\"5\">5 — ยาตัวอย่างเพื่อทดลองใช้</option>", html);
        Assert.Equal(6, Regex.Matches(html, "<option value=").Count);          // ทุกประเภท + 5 live types, nowhere else
        Assert.DoesNotContain("สรุปตามประเภท", html);
        // row metadata carries EDNAME under the name (muted line, no extra column)
        Assert.Contains("class=\"ds-item-meta reorder-item-type\">ยาในบัญชียาหลักแห่งชาติ</div>", html);
        Assert.DoesNotContain("<th scope=\"col\">ประเภท", html);

        var filtered = await GetAsync("/Reorder?status=red&type=1&q=cap");
        Assert.Contains("<option value=\"1\" selected=\"selected\">", filtered);
        Assert.Single(Regex.Matches(filtered, "<tr class=\"ds-row reorder-row"));   // Clindamycin: red + type 1 + "cap"; Gabapentin is type 2
        Assert.Contains("1000130", filtered);
        Assert.DoesNotContain("1000250", filtered);
        Assert.Contains("ต้องสั่งซื้อทันที</strong> · ยาในบัญชียาหลักแห่งชาติ · 1 รายการ · ค้นหา “cap”", Regex.Replace(filtered, @"\s+", " "));
        // status tabs, print and back links keep the type; counts describe the type
        var tabs = Regex.Matches(filtered, "<a[^>]*class=\"reorder-tab[^\"]*\"[^>]*href=\"([^\"]+)\"");
        Assert.Equal(4, tabs.Count);
        Assert.All(tabs.Cast<Match>(), m => { Assert.Contains("type=1", m.Groups[1].Value); Assert.Contains("q=cap", m.Groups[1].Value); });
        var print = Regex.Match(filtered, "<a[^>]*href=\"(/Reorder/Print[^\"]*)\"[^>]*>");
        Assert.Contains("type=1", print.Groups[1].Value); Assert.Contains("status=red", print.Groups[1].Value); Assert.Contains("q=cap", print.Groups[1].Value);
        // ล้าง clears keyword + type but keeps the status
        var clear = Regex.Match(filtered, "<a class=\"ds-btn ds-btn-secondary\" href=\"([^\"]+)\">ล้าง</a>");
        Assert.True(clear.Success); Assert.Contains("status=red", clear.Groups[1].Value); Assert.DoesNotContain("type=", clear.Groups[1].Value); Assert.DoesNotContain("q=", clear.Groups[1].Value);
        // unknown type value → treated as ทุกประเภท
        Assert.Equal(6, Regex.Matches(await GetAsync("/Reorder?status=all&type=zz"), "<tr class=\"ds-row reorder-row").Count);

        var report = await GetAsync("/Reorder/Print?status=all&type=3");
        Assert.Contains("<dt>ประเภทเวชภัณฑ์</dt><dd>วัสดุการแพทย์</dd>", report);
        Assert.Single(Regex.Matches(report, "<tr>\\s*<td class=\"num muted\">"));
        Assert.Contains("3000860", report);
    }

    [Fact]
    public async Task Missing_thresholds_are_secondary_text_not_a_badge_wall()
    {
        var html = await GetAsync("/Reorder?status=all");
        Assert.Contains("ยังไม่กำหนด MIN/MAX", html);
        Assert.DoesNotContain("ไม่ได้กำหนด MIN/MAX</span>", html);
        Assert.Contains("class=\"reorder-context-warning\">ยังไม่กำหนด MIN 2 รายการ · MAX 2 รายการ</p>", html);
    }

    [Fact]
    public void Reorder_css_has_no_inner_scroll_and_no_full_row_tint()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Invc.slnx"))) dir = dir.Parent;
        var css = File.ReadAllText(Path.Combine(dir!.FullName, "src", "Invc.Web", "wwwroot", "css", "site.css"));
        var start = css.IndexOf("Reorder (คำแนะนำการสั่งซื้อ)", StringComparison.Ordinal);
        var end = css.IndexOf("Borrow (", start, StringComparison.Ordinal);
        var block = css[start..end];
        Assert.DoesNotContain("max-height", block);
        Assert.DoesNotContain("overflow: auto", block);
        Assert.DoesNotContain("overflow-y", block);
        Assert.DoesNotContain("overflow-x", block);
        Assert.DoesNotContain("reorder-row-attention td { background", block);   // old full-row pink tint
        Assert.DoesNotMatch(@"\.reorder-row\.is-red(\s*>\s*td)?\s*\{[^}]*background", block);
        Assert.Contains("box-shadow: inset 3px 0 0 var(--danger)", block);       // thin accent only
    }
}
