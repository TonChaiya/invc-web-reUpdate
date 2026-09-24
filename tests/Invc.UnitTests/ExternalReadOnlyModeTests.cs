using System.Net;
using System.Text.RegularExpressions;
using Invc.Web;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Invc.UnitTests;

/// <summary>
/// External (DDNS) hosting of the same release: anonymous, read-only. Two hosts over the SAME in-memory stores — the
/// internal one (Windows authentication in production, full workflow) primes the data, the external one may only read
/// it. The guard is server-side: hidden buttons are never the protection, so every test posts the real endpoint.
/// </summary>
public sealed class ExternalReadOnlyModeTests : IDisposable
{
    private readonly BorrowPageUiTests.Factory _internal = new();
    private readonly BorrowPageUiTests.Factory _external;
    private readonly HttpClient _internalClient;
    private readonly HttpClient _externalClient;

    public ExternalReadOnlyModeTests()
    {
        _external = new BorrowPageUiTests.Factory(_internal, externalReadOnly: true);
        _internalClient = _internal.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        _externalClient = _external.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    public void Dispose()
    {
        _internalClient.Dispose(); _externalClient.Dispose();
        _external.Dispose(); _internal.Dispose();
    }

    /// <summary>The internal host reconciles the mirror from INV; afterwards both hosts see the same bills.</summary>
    private async Task PrimeAsync() => Assert.Equal(HttpStatusCode.OK, (await _internalClient.GetAsync("/Borrow")).StatusCode);

    [Theory]
    [InlineData("/")]
    [InlineData("/Inventory/Status")]
    [InlineData("/Reorder")]
    [InlineData("/PurchaseOrders")]
    [InlineData("/Receipts")]
    [InlineData("/Borrow")]
    [InlineData("/Borrow/History")]
    public async Task Read_pages_stay_available_externally(string url)
    {
        await PrimeAsync();
        using var response = await _externalClient.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Health_is_reachable_externally_and_never_refused()
    {
        using var response = await _externalClient.GetAsync("/Health");
        // The test host has no reachable INV, so the page legitimately reports 503; what matters is that the
        // read-only guard does not refuse it.
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("สถานะ", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task External_pages_show_the_read_only_banner_and_no_return_controls()
    {
        await PrimeAsync();
        var external = WebUtility.HtmlDecode(await _externalClient.GetStringAsync("/Borrow/Facility/CUB001?status=all"));
        Assert.Contains(ExternalAccessMode.Banner, external);
        Assert.DoesNotContain("บันทึกคืน", external);
        Assert.DoesNotContain("คืนครบทั้งบิล", external);
        Assert.DoesNotContain("ระบุวันที่/หมายเหตุ", external);
        Assert.Contains("คงเหลือ", external);                                   // the data itself is still there

        var inside = WebUtility.HtmlDecode(await _internalClient.GetStringAsync("/Borrow/Facility/CUB001?status=all"));
        Assert.DoesNotContain(ExternalAccessMode.Banner, inside);
        Assert.Contains("บันทึกคืน", inside);
        Assert.Contains("คืนครบทั้งบิล", inside);
    }

    [Fact]
    public async Task External_return_posts_are_refused_and_create_no_event()
    {
        await PrimeAsync();
        var token = await InternalTokenAsync();

        foreach (var (url, fields) in new (string, Dictionary<string, string>)[]
        {
            ("/Borrow/Facility/CUB001?handler=Return", new() { ["Quick.ItemRecordNumber"] = "1036", ["Quick.BillRecordNumber"] = "113", ["Quick.Quantity"] = "10", ["Quick.ClientRequestId"] = Guid.NewGuid().ToString("D") }),
            ("/Borrow/Facility/CUB001?handler=ReturnBill&billRecordNumber=113", new() { ["clientRequestId"] = Guid.NewGuid().ToString("D") }),
            ("/Borrow/Return/1036", new() { ["Form.Quantity"] = "10", ["Form.ClientRequestId"] = Guid.NewGuid().ToString("D") }),
            ("/Borrow/ReturnBill/113", new() { ["Form.ClientRequestId"] = Guid.NewGuid().ToString("D") }),
            ("/Borrow/Correct/1", new() { ["Form.Quantity"] = "10", ["Form.ClientRequestId"] = Guid.NewGuid().ToString("D") }),
        })
        {
            fields["__RequestVerificationToken"] = token;
            using var response = await _externalClient.PostAsync(url, new FormUrlEncodedContent(fields));
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Contains("โหมดดูข้อมูลเท่านั้น", await response.Content.ReadAsStringAsync());
        }

        Assert.Empty(_internal.Returns.Events);
    }

    [Fact]
    public async Task External_mutation_pages_are_refused_even_for_GET()
    {
        await PrimeAsync();
        foreach (var url in new[] { "/Borrow/Return/1036", "/Borrow/ReturnBill/113", "/Borrow/Correct/1" })
        {
            using var response = await _externalClient.GetAsync(url);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        // the equivalent internal pages stay available
        Assert.Equal(HttpStatusCode.OK, (await _internalClient.GetAsync("/Borrow/Return/1036")).StatusCode);
    }

    [Fact]
    public async Task External_browsing_never_writes_the_borrow_mirror()
    {
        await PrimeAsync();
        var applyCalls = _internal.Mirror.ApplyCalls;
        Assert.True(applyCalls > 0, "the internal host reconciles the mirror");

        foreach (var url in new[] { "/Borrow", "/Borrow?status=all", "/Borrow/Facility/CUB001?status=all", "/Borrow/History" })
        {
            Assert.Equal(HttpStatusCode.OK, (await _externalClient.GetAsync(url)).StatusCode);
        }

        Assert.Equal(applyCalls, _internal.Mirror.ApplyCalls);                  // no reconciliation write from the external site
        Assert.Empty(_internal.Returns.Events);
    }

    [Fact]
    public void Guard_refuses_every_unsafe_method_and_allows_reads()
    {
        var external = new ExternalAccessMode(true);
        foreach (var method in new[] { "POST", "PUT", "PATCH", "DELETE" })
        {
            Assert.True(external.Denies(method, "/Anything"), method);
        }
        Assert.True(external.Denies("GET", "/Borrow/Return/1"));
        Assert.True(external.Denies("GET", "/Borrow/ReturnBill/1"));
        Assert.True(external.Denies("GET", "/Borrow/Correct/1"));
        Assert.False(external.Denies("GET", "/Borrow/History"));
        Assert.False(external.Denies("HEAD", "/Inventory/Status"));

        var inside = new ExternalAccessMode(false);
        Assert.False(inside.Denies("POST", "/Borrow/Return/1"));
    }

    private async Task<string> InternalTokenAsync()
    {
        var html = await _internalClient.GetStringAsync("/Borrow/Facility/CUB001?status=all");
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(token), "antiforgery token expected");
        return token;
    }
}
