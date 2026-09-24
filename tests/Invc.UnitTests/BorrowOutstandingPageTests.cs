using System.Net;
using System.Text.RegularExpressions;
using Invc.Web;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Invc.UnitTests;

/// <summary>
/// สรุปคงค้าง through the real Razor pipeline: the aggregated review screen, its drill-down and the fact that it never
/// offers a write — internally or on the external anonymous site.
/// </summary>
public sealed class BorrowOutstandingPageTests : IDisposable
{
    private readonly BorrowPageUiTests.Factory _factory = new();
    private readonly HttpClient _client;

    public BorrowOutstandingPageTests() { _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }); }
    public void Dispose() { _client.Dispose(); _factory.Dispose(); }

    private static string Text(string html) => Regex.Replace(Regex.Replace(html, "<[^>]+>", " "), @"\s+", " ").Trim();
    private async Task<string> GetAsync(string url) => WebUtility.HtmlDecode(await _client.GetStringAsync(url));

    [Fact]
    public async Task Summary_groups_by_facility_and_shows_totals_and_drill_down()
    {
        var html = await GetAsync("/Borrow/Outstanding");

        // the two top-level views, with this one current
        Assert.Contains("สถานบริการ", html);
        Assert.Contains("aria-current=\"page\"", Regex.Match(html, "<a class=\"borrow-view is-active\"[^>]*>").Value);

        // one section per facility, linking into the operational screen
        Assert.Contains("/Borrow/Facility/CUB001?status=outstanding", html);
        Assert.Contains("/Borrow/Facility/PAO001?status=outstanding", html);
        Assert.Equal(2, Regex.Matches(html, "class=\"borrow-bill borrow-osum-facility\"").Count);

        // CUB001: three outstanding medicines (250 + 100 + 400 = 750)
        var cub = Regex.Match(html, "<section class=\"borrow-bill borrow-osum-facility\" aria-label=\"ศูนย์.*?</section>", RegexOptions.Singleline).Value;
        Assert.Equal(3, Regex.Matches(cub, "class=\"borrow-osum-row\"").Count);
        var total = Text(Regex.Match(cub, "<div class=\"borrow-osum-row borrow-osum-total\">.*?</div>\\s*</section>", RegexOptions.Singleline).Value);
        Assert.Contains("750", total);

        // drill-down is a native disclosure with the source bill rows
        Assert.Contains("<details class=\"borrow-osum-bills\">", cub);
        Assert.Contains("ดู 1 บิล", cub);
        Assert.Contains("O6900084", cub);

        // overall strip
        var strip = Text(Regex.Match(html, "<div class=\"borrow-summary\".*?</div>", RegexOptions.Singleline).Value);
        Assert.Contains("สถานบริการ 2", strip);
        Assert.Contains("รายการยา 4", strip);                                  // 3 in CUB001 + 1 in PAO001
        Assert.Contains("บิล 3", strip);
        Assert.Contains("ต้องคืน 755", strip);
    }

    [Fact]
    public async Task Fully_returned_work_disappears_from_the_summary()
    {
        Assert.Contains("ซอง sterile", await GetAsync("/Borrow/Outstanding"));                 // PAO001 item 500, qty 5

        var (token, cid) = await ReturnFormAsync("/Borrow/Return/500");
        var post = await _client.PostAsync("/Borrow/Return/500?handler=Full",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["Form.Quantity"] = "5", ["Form.ClientRequestId"] = cid, ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);

        var html = await GetAsync("/Borrow/Outstanding");
        Assert.DoesNotContain("ซอง sterile", html);
        Assert.DoesNotContain("PAO001", html);
        Assert.Contains("สถานบริการ 1", Text(Regex.Match(html, "<div class=\"borrow-summary\".*?</div>", RegexOptions.Singleline).Value));
    }

    [Fact]
    public async Task Search_narrows_the_summary_and_its_totals()
    {
        var html = await GetAsync("/Borrow/Outstanding?q=Albendazole");
        Assert.Contains("Albendazole", html);
        Assert.DoesNotContain("Acyclovir", html);
        var strip = Text(Regex.Match(html, "<div class=\"borrow-summary\".*?</div>", RegexOptions.Singleline).Value);
        Assert.Contains("รายการยา 1", strip);
        Assert.Contains("ต้องคืน 400", strip);

        Assert.Contains("ไม่พบรายการค้างคืนที่ตรงกับ", await GetAsync("/Borrow/Outstanding?q=ไม่มีจริง"));
    }

    [Fact]
    public async Task Summary_offers_no_write_control()
    {
        var html = await GetAsync("/Borrow/Outstanding");
        Assert.DoesNotContain("<form method=\"post\"", html);
        Assert.DoesNotContain("บันทึกคืน", html);
        Assert.DoesNotContain("คืนครบทั้งบิล", html);
        Assert.DoesNotContain("/Borrow/Correct", html);
    }

    [Fact]
    public async Task Summary_is_readable_on_the_external_read_only_site_and_still_refuses_writes()
    {
        await _client.GetAsync("/Borrow");                                                     // internal host primes the mirror
        using var external = new BorrowPageUiTests.Factory(_factory, externalReadOnly: true);
        using var externalClient = external.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var html = WebUtility.HtmlDecode(await externalClient.GetStringAsync("/Borrow/Outstanding"));
        Assert.Contains("ซอง sterile", html);
        Assert.Contains(ExternalAccessMode.Banner, html);
        Assert.DoesNotContain("บันทึกคืน", html);

        using var post = await externalClient.PostAsync("/Borrow/Outstanding", new FormUrlEncodedContent(new Dictionary<string, string>()));
        Assert.Equal(HttpStatusCode.Forbidden, post.StatusCode);
        Assert.Empty(_factory.Returns.Events);
    }

    private async Task<(string Token, string ClientRequestId)> ReturnFormAsync(string url)
    {
        var html = await _client.GetStringAsync(url);
        return (Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value,
                Regex.Match(html, "name=\"Form.ClientRequestId\" value=\"([^\"]+)\"").Groups[1].Value);
    }
}
