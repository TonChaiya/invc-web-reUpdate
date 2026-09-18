using System.Net;
using System.Text.RegularExpressions;
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
            new() { WorkingCode = "1000130", DrugName = "Clindamycin 300 mg cap", QtyOnHand = 0, MinLevel = 33, ReorderQty = 33, MaxLevel = 50, SaleUnit = "CAP", RatePerMonth = 10 },
            new() { WorkingCode = "1000250", DrugName = "Gabapentin 300 mg cap", QtyOnHand = 200, MinLevel = 433, ReorderQty = 433, MaxLevel = 650, SaleUnit = "CAP", RatePerMonth = 433 },
            new() { WorkingCode = "1000500", DrugName = "Yellow test item", QtyOnHand = 100, MinLevel = 100, ReorderQty = 150, MaxLevel = 200, SaleUnit = "TAB", RatePerMonth = 50 },
            new() { WorkingCode = "1000010", DrugName = "Acyclovir 400 mg tab", QtyOnHand = 100, MinLevel = 100, ReorderQty = 100, MaxLevel = 150, SaleUnit = "TAB", RatePerMonth = 100 },
            new() { WorkingCode = "1000020", DrugName = "Albendazole 200 mg tab", QtyOnHand = 100, MinLevel = 0, ReorderQty = 0, MaxLevel = 0, SaleUnit = "TAB", RatePerMonth = 0 },
            new() { WorkingCode = "3000860", DrugName = "ซอง sterile 6 นิ้ว", QtyOnHand = 0, MinLevel = 0, ReorderQty = 0, MaxLevel = 0, SaleUnit = "ซอง", RatePerMonth = null },
        ];

        public Task<IReadOnlyList<ReorderItem>> GetEligibleAsync(string? keyword, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ReorderItem> result = keyword is null
                ? Items
                : Items.Where(i => i.DrugName.Contains(keyword, StringComparison.OrdinalIgnoreCase) || i.WorkingCode.Contains(keyword)).ToList();
            return Task.FromResult(result);
        }
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
    public async Task Summary_card_grid_is_gone()
    {
        var html = await GetAsync("/Reorder");
        Assert.DoesNotContain("inv-cards", html);
        Assert.DoesNotContain("reorder-card-active", html);
        Assert.DoesNotContain("fs-4 fw-semibold", html);
    }

    [Fact]
    public async Task Status_strip_lists_every_status_with_its_count_and_marks_the_current_one()
    {
        var html = await GetAsync("/Reorder?status=green");
        var strip = Regex.Match(html, "<nav[^>]*class=\"[^\"]*reorder-status-strip[^\"]*\"[^>]*>(.*?)</nav>", RegexOptions.Singleline);
        Assert.True(strip.Success, "one <nav class=\"reorder-status-strip\"> expected");
        var options = Regex.Matches(strip.Groups[1].Value, "<a[^>]*class=\"[^\"]*reorder-status-option[^\"]*\"[^>]*>(.*?)</a>", RegexOptions.Singleline);
        Assert.Equal(4, options.Count);

        string Text(Match m) => Regex.Replace(m.Groups[1].Value, "<[^>]+>", " ");
        Assert.Matches(@"ต้องสั่งซื้อทันที\s+2\b", Regex.Replace(Text(options[0]), @"\s+", " "));
        Assert.Matches(@"ใกล้ถึงจุดสั่งซื้อ\s+1\b", Regex.Replace(Text(options[1]), @"\s+", " "));
        Assert.Matches(@"มีสำรอง\s+3\b", Regex.Replace(Text(options[2]), @"\s+", " "));
        Assert.Matches(@"ทั้งหมด\s+6\b", Regex.Replace(Text(options[3]), @"\s+", " "));

        // accessible current state on the selected option only
        var current = options.Cast<Match>().Where(m => m.Value.Contains("aria-current=\"page\"")).ToList();
        Assert.Single(current);
        Assert.Contains("status=green", current[0].Value);
    }

    [Fact]
    public async Task Switching_status_preserves_the_search_keyword_and_search_preserves_the_status()
    {
        var html = await GetAsync("/Reorder?status=all&q=cap");
        // status links keep q
        var links = Regex.Matches(html, "<a[^>]*class=\"[^\"]*reorder-status-option[^\"]*\"[^>]*href=\"([^\"]+)\"");
        Assert.Equal(4, links.Count);
        Assert.All(links.Cast<Match>(), m => Assert.Contains("q=cap", m.Groups[1].Value));
        // search form keeps status via hidden field
        var form = Regex.Match(html, "<form[^>]*class=\"[^\"]*reorder-toolbar[^\"]*\"[^>]*>(.*?)</form>", RegexOptions.Singleline);
        Assert.True(form.Success, "one <form class=\"reorder-toolbar\"> expected");
        Assert.Contains("name=\"status\" value=\"all\"", form.Groups[1].Value.Replace("' ", "\" ").Replace("'", "\""));
        Assert.Contains("placeholder=\"รหัสยา / ชื่อยา / ส่วนประกอบ\"", form.Groups[1].Value);
    }

    [Fact]
    public async Task Print_link_preserves_status_and_search()
    {
        var html = await GetAsync("/Reorder?status=green&q=tab");
        var print = Regex.Match(html, "<a[^>]*href=\"(/Reorder/Print[^\"]*)\"[^>]*>");
        Assert.True(print.Success, "print link expected");
        Assert.Contains("status=green", print.Groups[1].Value);
        Assert.Contains("q=tab", print.Groups[1].Value);
    }

    [Fact]
    public async Task Result_table_is_present_with_status_text_and_suggested_quantity()
    {
        var html = await GetAsync("/Reorder");   // default = red
        Assert.Contains("class=\"table", html);
        Assert.Contains("reorder-table", html);
        Assert.Contains("ต้องสั่งซื้อทันที", html);      // status communicated as text, never colour only
        Assert.Contains("reorder-suggested", html);       // suggested quantity has its own hook
        Assert.Contains("1000130", html);
        Assert.Contains("Clindamycin 300 mg cap", html);
        Assert.DoesNotContain("SqlException", html);
    }

    [Fact]
    public async Task Missing_thresholds_are_secondary_text_not_a_badge_wall()
    {
        var html = await GetAsync("/Reorder?status=all");
        Assert.Contains("ยังไม่กำหนด MIN/MAX", html);
        Assert.DoesNotContain("ไม่ได้กำหนด MIN/MAX</span>", html);   // old badge markup gone
    }
}
