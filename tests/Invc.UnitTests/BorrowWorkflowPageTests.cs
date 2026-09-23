using System.Net;
using System.Text.RegularExpressions;
using Invc.Core.Borrow;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Invc.UnitTests;

/// <summary>
/// Return workflow through the real Razor pipeline (antiforgery, model binding, PRG) with the in-memory source / mirror /
/// return store of <see cref="BorrowPageUiTests.Factory"/>. Each test class instance gets its own factory so trails do not leak.
/// </summary>
public sealed class BorrowWorkflowPageTests : IDisposable
{
    private readonly BorrowPageUiTests.Factory _factory = new();
    private readonly HttpClient _client;

    public BorrowWorkflowPageTests() { _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }); }
    public void Dispose() { _client.Dispose(); _factory.Dispose(); }

    private static string Text(string fragment) => Regex.Replace(Regex.Replace(fragment, "<[^>]+>", " "), @"\s+", " ").Trim();

    private async Task<(string Html, string Token, string ClientRequestId)> GetFormAsync(string url)
    {
        var r = await _client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var html = await r.Content.ReadAsStringAsync();
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        var cid = Regex.Match(html, "name=\"Form.ClientRequestId\" value=\"([^\"]+)\"").Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(token), "antiforgery token expected"); Assert.Equal(36, cid.Length);
        return (WebUtility.HtmlDecode(html), token, cid);
    }

    private Task<HttpResponseMessage> PostAsync(string url, string token, Dictionary<string, string> fields)
    {
        fields["__RequestVerificationToken"] = token;
        return _client.PostAsync(url, new FormUrlEncodedContent(fields));
    }

    private async Task PrimeAsync() { Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/Borrow")).StatusCode); }   // sync → mirror populated

    [Fact]
    public async Task Index_defaults_to_outstanding_filter_and_shows_borrowed_returned_outstanding_per_facility()
    {
        await PrimeAsync();
        var html = WebUtility.HtmlDecode(await _client.GetStringAsync("/Borrow"));
        Assert.Contains("aria-current=\"page\"", Regex.Match(html, "<a class=\"borrow-tab is-active is-outstanding\"[^>]*>").Value);
        Assert.Contains("คงค้าง", html); Assert.Contains("คืนแล้ว", html);
        Assert.Equal(2, Regex.Matches(html, "class=\"borrow-facility-row").Count);
        Assert.Contains("status-chip status-outstanding\">ค้างคืน", html);
        // returned filter: nothing returned yet
        var returned = WebUtility.HtmlDecode(await _client.GetStringAsync("/Borrow?status=returned"));
        Assert.Contains("ไม่มีสถานบริการในสถานะ “คืนครบ”", returned);
        Assert.Contains("href=\"/Borrow/History\"", html);
    }

    [Fact]
    public async Task Partial_return_then_full_item_return_update_figures_status_and_history_with_PRG()
    {
        await PrimeAsync();
        var (form, token, cid) = await GetFormAsync("/Borrow/Return/1036");
        Assert.Contains("AMILORIDE + HCTZ (5+50MG) MODURETIC TAB", form);
        Assert.Contains("value=\"250\"", form);                                          // default = current outstanding
        Assert.Contains("คืนครบรายการ (250)", form);

        var post = await PostAsync("/Borrow/Return/1036", token, new() { ["Form.Quantity"] = "100", ["Form.EventAt"] = "2026-09-20T10:30", ["Form.Note"] = "คืนรอบแรก", ["Form.ClientRequestId"] = cid });
        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
        Assert.StartsWith("/Borrow/Facility/CUB001?status=all#item-1036", post.Headers.Location!.ToString());
        var ev = Assert.Single(_factory.Returns.Events);
        Assert.Equal(100m, ev.QuantityDelta); Assert.Equal(BorrowReturnEventType.Return, ev.EventType); Assert.Equal("คืนรอบแรก", ev.Note);
        Assert.StartsWith("anonymous:", ev.Actor);                                        // no auth in the test host → documented fallback
        Assert.Equal(new DateTime(2026, 9, 20, 10, 30, 0), ev.EventAt);

        var facility = WebUtility.HtmlDecode(await _client.GetStringAsync("/Borrow/Facility/CUB001?status=all"));
        var row = Regex.Match(facility, "<div class=\"borrow-item-row[^\"]*\" id=\"item-1036\">(.*?)</dl>", RegexOptions.Singleline).Groups[1].Value;
        var t = Text(row);
        Assert.Contains("ยืม 250", t); Assert.Contains("คืนแล้ว 100", t); Assert.Contains("คงเหลือ 150", t);
        Assert.Contains("คืนบางส่วน", facility);
        Assert.Contains("บันทึกคืน AMILORIDE + HCTZ (5+50MG) MODURETIC TAB จำนวน 100 เรียบร้อย", facility);   // TempData notice after PRG

        // refresh / double submit with the same client request id does not create a second event
        var again = await PostAsync("/Borrow/Return/1036", token, new() { ["Form.Quantity"] = "100", ["Form.ClientRequestId"] = cid });
        Assert.Equal(HttpStatusCode.Redirect, again.StatusCode);
        Assert.Single(_factory.Returns.Events);
        Assert.Contains("ไม่บันทึกซ้ำ", WebUtility.HtmlDecode(await _client.GetStringAsync("/Borrow/Facility/CUB001?status=all")));

        // full item return via the "คืนครบรายการ" handler: exact current outstanding (150), server-side
        var (_, token2, cid2) = await GetFormAsync("/Borrow/Return/1036");
        var full = await PostAsync("/Borrow/Return/1036?handler=Full", token2, new() { ["Form.Quantity"] = "999", ["Form.ClientRequestId"] = cid2 });
        Assert.Equal(HttpStatusCode.Redirect, full.StatusCode);
        Assert.Equal(2, _factory.Returns.Events.Count); Assert.Equal(150m, _factory.Returns.Events[1].QuantityDelta);

        var done = WebUtility.HtmlDecode(await _client.GetStringAsync("/Borrow/Facility/CUB001?status=all"));
        var doneRow = Text(Regex.Match(done, "<div class=\"borrow-item-row[^\"]*\" id=\"item-1036\">(.*?)</section>", RegexOptions.Singleline).Groups[1].Value);
        Assert.Contains("คงเหลือ 0", doneRow); Assert.Contains("คืนครบ", doneRow); Assert.DoesNotContain("บันทึกคืน", doneRow);
        // the returned item no longer offers the return page form
        var closed = WebUtility.HtmlDecode(await _client.GetStringAsync("/Borrow/Return/1036"));
        Assert.Contains("รายการนี้คืนครบแล้ว", closed);
    }

    private async Task<(string Token, string ClientRequestId, string Action)> InlineFormAsync(int itemRecordNumber, string url = "/Borrow/Facility/CUB001?status=all")
    {
        var html = await _client.GetStringAsync(url);
        var row = Regex.Match(html, "<div class=\"borrow-item-row[^\"]*\" id=\"item-" + itemRecordNumber + "\">.*?</form>", RegexOptions.Singleline).Value;
        var form = Regex.Match(row, "<form[^>]*class=\"borrow-quick-return\".*?</form>", RegexOptions.Singleline).Value;
        Assert.Contains("method=\"post\"", form);
        return (Regex.Match(form, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value,
                Regex.Match(form, "name=\"Quick.ClientRequestId\" value=\"([^\"]+)\"").Groups[1].Value,
                WebUtility.HtmlDecode(Regex.Match(form, "action=\"([^\"]+)\"").Groups[1].Value));   // browsers decode &amp; in the action
    }

    [Fact]
    public async Task Inline_return_updates_the_page_in_place_without_navigating()
    {
        await PrimeAsync();
        var (token, cid, action) = await InlineFormAsync(1036);
        Assert.Contains("handler=Return", action);

        var request = new HttpRequestMessage(HttpMethod.Post, action)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Quick.ItemRecordNumber"] = "1036", ["Quick.BillRecordNumber"] = "113", ["Quick.Quantity"] = "50",
                ["Quick.ClientRequestId"] = cid, ["__RequestVerificationToken"] = token,
            }),
        };
        request.Headers.Add("X-Requested-With", "fetch");
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);                       // a fragment, not a redirect
        var fragment = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.DoesNotContain("<html", fragment);                                    // partial only: the shell is not re-sent
        Assert.Contains("บันทึกคืน 50 เรียบร้อย", fragment);
        Assert.Contains("id=\"item-1036\"", fragment);
        Assert.Contains("is-updated", fragment);                                     // the row is highlighted after the swap

        var ev = Assert.Single(_factory.Returns.Events);
        Assert.Equal(50m, ev.QuantityDelta); Assert.Equal(BorrowReturnEventType.Return, ev.EventType);
        Assert.StartsWith("anonymous:", ev.Actor);                                   // server-side actor, never the form
        Assert.Null(ev.Note);

        var row = Text(Regex.Match(fragment, "id=\"item-1036\">(.*?)</dl>", RegexOptions.Singleline).Groups[1].Value);
        Assert.Contains("ยืม 250", row); Assert.Contains("คืนแล้ว 50", row); Assert.Contains("คงเหลือ 200", row);
        Assert.Contains("คืนบางส่วน", fragment);
        // facility and bill totals in the same fragment are recomputed
        var summary = Text(Regex.Match(fragment, "<div class=\"borrow-summary\".*?</div>", RegexOptions.Singleline).Value);
        Assert.Contains("คืนแล้ว 50", summary); Assert.Contains("คงค้าง 700", summary);   // CUB001: 750 borrowed − 50 returned

        // the same client request id (double click / retry) does not write twice
        var again = new HttpRequestMessage(HttpMethod.Post, action)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Quick.ItemRecordNumber"] = "1036", ["Quick.BillRecordNumber"] = "113", ["Quick.Quantity"] = "50",
                ["Quick.ClientRequestId"] = cid, ["__RequestVerificationToken"] = token,
            }),
        };
        again.Headers.Add("X-Requested-With", "fetch");
        var second = await _client.SendAsync(again);
        Assert.Contains("ไม่บันทึกซ้ำ", WebUtility.HtmlDecode(await second.Content.ReadAsStringAsync()));
        Assert.Single(_factory.Returns.Events);
    }

    [Fact]
    public async Task Inline_bill_return_records_every_open_item_in_place()
    {
        await PrimeAsync();
        var (token, _, _) = await InlineFormAsync(900);
        var html = await _client.GetStringAsync("/Borrow/Facility/CUB001?status=all");
        // the bill action degrades to the confirmation page without JavaScript, and carries the data the script needs
        var link = Regex.Matches(html, "<a class=\"borrow-inline-action borrow-bill-return\"[^>]*>").Cast<Match>().Single(m => m.Value.Contains("data-bill=\"100\"")).Value;
        Assert.Contains("href=\"/Borrow/ReturnBill/100\"", link);
        Assert.Contains("data-bill=\"100\"", link);
        Assert.Contains("data-outstanding=\"500\"", link);
        Assert.Contains("id=\"borrow-antiforgery\"", html);

        var request = new HttpRequestMessage(HttpMethod.Post, "/Borrow/Facility/CUB001?status=all&handler=ReturnBill")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["billRecordNumber"] = "100", ["clientRequestId"] = Guid.NewGuid().ToString("D"), ["__RequestVerificationToken"] = token,
            }),
        };
        request.Headers.Add("X-Requested-With", "fetch");
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fragment = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.DoesNotContain("<html", fragment);
        Assert.Contains("บันทึกคืนครบทั้งบิล (2 รายการ) เรียบร้อย", fragment);
        Assert.Equal(2, _factory.Returns.Events.Count);
        Assert.Equal(500m, _factory.Returns.Events.Sum(e => e.QuantityDelta));
        var summary = Text(Regex.Match(fragment, "<div class=\"borrow-summary\".*?</div>", RegexOptions.Singleline).Value);
        Assert.Contains("คืนแล้ว 500", summary); Assert.Contains("คงค้าง 250", summary);
    }

    [Fact]
    public async Task Inline_return_validates_server_side_and_falls_back_to_redirect_without_javascript()
    {
        await PrimeAsync();
        var (token, cid, action) = await InlineFormAsync(900);

        // over-return through the inline form is refused and reported inside the fragment
        var bad = new HttpRequestMessage(HttpMethod.Post, action)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Quick.ItemRecordNumber"] = "900", ["Quick.BillRecordNumber"] = "100", ["Quick.Quantity"] = "101",
                ["Quick.ClientRequestId"] = cid, ["__RequestVerificationToken"] = token,
            }),
        };
        bad.Headers.Add("X-Requested-With", "fetch");
        var badResponse = await _client.SendAsync(bad);
        Assert.Equal(HttpStatusCode.OK, badResponse.StatusCode);
        Assert.Contains("คืนได้ไม่เกินยอดคงเหลือ 100", WebUtility.HtmlDecode(await badResponse.Content.ReadAsStringAsync()));
        Assert.Empty(_factory.Returns.Events);

        // an item that does not belong to the posted bill is refused
        var mismatch = await PostAsync(action, token, new() { ["Quick.ItemRecordNumber"] = "900", ["Quick.BillRecordNumber"] = "113", ["Quick.Quantity"] = "1", ["Quick.ClientRequestId"] = Guid.NewGuid().ToString("D") });
        Assert.Equal(HttpStatusCode.Redirect, mismatch.StatusCode);                  // no AJAX header → PRG fallback
        Assert.Empty(_factory.Returns.Events);

        // plain (no-JavaScript) submit succeeds and redirects back to the same filtered view
        var (token2, cid2, action2) = await InlineFormAsync(900);
        var plain = await PostAsync(action2, token2, new() { ["Quick.ItemRecordNumber"] = "900", ["Quick.BillRecordNumber"] = "100", ["Quick.Quantity"] = "40", ["Quick.ClientRequestId"] = cid2 });
        Assert.Equal(HttpStatusCode.Redirect, plain.StatusCode);
        Assert.StartsWith("/Borrow/Facility/CUB001?status=all", plain.Headers.Location!.ToString());
        Assert.Contains("#item-900", plain.Headers.Location!.ToString());
        Assert.Equal(40m, Assert.Single(_factory.Returns.Events).QuantityDelta);

        // and without an antiforgery token nothing is written
        var forged = await _client.PostAsync(action2, new FormUrlEncodedContent(new Dictionary<string, string> { ["Quick.ItemRecordNumber"] = "900", ["Quick.BillRecordNumber"] = "100", ["Quick.Quantity"] = "1", ["Quick.ClientRequestId"] = Guid.NewGuid().ToString("D") }));
        Assert.Equal(HttpStatusCode.BadRequest, forged.StatusCode);
        Assert.Single(_factory.Returns.Events);
    }

    [Fact]
    public async Task Return_forms_default_the_date_to_now_in_a_value_browsers_accept()
    {
        // production defect 2026-09-23: the app runs under th-TH (Buddhist calendar), so a culture-formatted value
        // rendered "2569-09-23T09:54" and the browser showed an empty date. HTML date controls are Gregorian-only.
        await PrimeAsync();
        foreach (var url in new[] { "/Borrow/Return/1036", "/Borrow/ReturnBill/100" })
        {
            var html = WebUtility.HtmlDecode(await _client.GetStringAsync(url));
            var input = Regex.Match(html, "<input[^>]*id=\"at\"[^>]*>").Value;
            Assert.Contains("type=\"datetime-local\"", input);
            var value = Regex.Match(input, "value=\"([^\"]+)\"").Groups[1].Value;
            var max = Regex.Match(input, "max=\"([^\"]+)\"").Groups[1].Value;
            foreach (var v in new[] { value, max })
            {
                Assert.Matches(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$", v);
                Assert.True(DateTime.TryParseExact(v, "yyyy-MM-dd'T'HH:mm", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsed), v);
                Assert.Equal(DateTime.Now.Year, parsed.Year);            // Gregorian year, not 2569
                Assert.Equal(DateTime.Now.Date, parsed.Date);            // defaults to today
            }
            Assert.Contains("name=\"Form.EventAt\"", input);            // still binds to the model
        }
        // history date filters use the same Gregorian formatting
        var history = WebUtility.HtmlDecode(await _client.GetStringAsync("/Borrow/History?from=2026-09-01&to=2026-09-30"));
        Assert.Contains("value=\"2026-09-01\"", history);
        Assert.Contains("value=\"2026-09-30\"", history);
    }

    [Fact]
    public async Task Over_return_and_invalid_quantities_are_rejected_server_side_without_writing()
    {
        await PrimeAsync();
        var (_, token, cid) = await GetFormAsync("/Borrow/Return/900");
        var over = await PostAsync("/Borrow/Return/900", token, new() { ["Form.Quantity"] = "101", ["Form.ClientRequestId"] = cid });
        Assert.Equal(HttpStatusCode.OK, over.StatusCode);
        Assert.Contains("คืนได้ไม่เกินยอดคงเหลือ 100", WebUtility.HtmlDecode(await over.Content.ReadAsStringAsync()));
        var zero = await PostAsync("/Borrow/Return/900", token, new() { ["Form.Quantity"] = "0", ["Form.ClientRequestId"] = cid });
        Assert.Equal(HttpStatusCode.OK, zero.StatusCode);
        Assert.Empty(_factory.Returns.Events);
        var unknown = await _client.GetAsync("/Borrow/Return/424242");
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task Post_without_antiforgery_token_is_refused()
    {
        await PrimeAsync();
        var r = await _client.PostAsync("/Borrow/Return/900", new FormUrlEncodedContent(new Dictionary<string, string> { ["Form.Quantity"] = "1", ["Form.ClientRequestId"] = Guid.NewGuid().ToString("D") }));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Empty(_factory.Returns.Events);
    }

    [Fact]
    public async Task Full_bill_return_requires_confirmation_and_returns_every_open_item_once()
    {
        await PrimeAsync();
        var (form, token, cid) = await GetFormAsync("/Borrow/ReturnBill/100");
        Assert.Contains("O6900050", form); Assert.Contains("Acyclovir 400 mg tab", form); Assert.Contains("Albendazole 200 mg tab", form);
        Assert.Contains("คงค้างที่จะบันทึกคืน", form); Assert.Contains(">500<", form);
        var noConfirm = await PostAsync("/Borrow/ReturnBill/100", token, new() { ["Form.ClientRequestId"] = cid, ["Form.Confirm"] = "false" });
        Assert.Equal(HttpStatusCode.OK, noConfirm.StatusCode);
        Assert.Contains("กรุณายืนยัน", WebUtility.HtmlDecode(await noConfirm.Content.ReadAsStringAsync()));
        Assert.Empty(_factory.Returns.Events);

        var ok = await PostAsync("/Borrow/ReturnBill/100", token, new() { ["Form.ClientRequestId"] = cid, ["Form.Confirm"] = "true", ["Form.Note"] = "คืนทั้งบิล" });
        Assert.Equal(HttpStatusCode.Redirect, ok.StatusCode);
        Assert.Equal(2, _factory.Returns.Events.Count);
        Assert.Equal(500m, _factory.Returns.Events.Sum(e => e.QuantityDelta));
        Assert.All(_factory.Returns.Events, e => Assert.Equal("คืนทั้งบิล", e.Note));

        var facility = WebUtility.HtmlDecode(await _client.GetStringAsync("/Borrow/Facility/CUB001?status=returned"));
        Assert.Contains("O6900050", facility); Assert.DoesNotContain("O6900084", facility);
        // bill now fully returned: no bill-level action
        var all = WebUtility.HtmlDecode(await _client.GetStringAsync("/Borrow/Facility/CUB001?status=all"));
        var billHeader = Regex.Match(all, "id=\"bill-100\">(.*?)</header>", RegexOptions.Singleline).Groups[1].Value;
        Assert.DoesNotContain("คืนครบทั้งบิล", billHeader); Assert.Contains("คืนครบ", Text(billHeader));
        Assert.Contains("บิลนี้คืนครบแล้ว", WebUtility.HtmlDecode(await _client.GetStringAsync("/Borrow/ReturnBill/100")));
    }

    [Fact]
    public async Task History_lists_events_newest_first_with_filters_and_correction_reverses_without_deleting()
    {
        await PrimeAsync();
        var (_, t1, c1) = await GetFormAsync("/Borrow/Return/900");
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync("/Borrow/Return/900", t1, new() { ["Form.Quantity"] = "40", ["Form.EventAt"] = "2026-09-18T09:00", ["Form.ClientRequestId"] = c1 })).StatusCode);
        var (_, t2, c2) = await GetFormAsync("/Borrow/Return/500");
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync("/Borrow/Return/500", t2, new() { ["Form.Quantity"] = "5", ["Form.EventAt"] = "2026-09-19T09:00", ["Form.ClientRequestId"] = c2 })).StatusCode);

        var history = WebUtility.HtmlDecode(await _client.GetStringAsync("/Borrow/History"));
        var rows = Regex.Matches(history, "<tr class=\"ds-row borrow-event ([^\"]+)\" id=\"event-(\\d+)\"");
        Assert.Equal(2, rows.Count);
        Assert.Equal("2", rows[0].Groups[2].Value);                                      // newest first (PAO001, 19 Sep)
        Assert.Contains("รพ.สต.บ้านต้นเปา", history); Assert.Contains("+5", history); Assert.Contains("+40", history);
        var byFacility = WebUtility.HtmlDecode(await _client.GetStringAsync("/Borrow/History?facility=CUB001"));
        Assert.Single(Regex.Matches(byFacility, "class=\"ds-row borrow-event"));
        Assert.Contains("Acyclovir", byFacility);
        var byItem = WebUtility.HtmlDecode(await _client.GetStringAsync("/Borrow/History?item=sterile"));
        Assert.Single(Regex.Matches(byItem, "class=\"ds-row borrow-event")); Assert.Contains("O6900010", byItem);
        Assert.Contains("ย้อนกลับ…", history);

        // correction: reason required, cannot exceed reversible, original event untouched
        var (cform, ct, cc) = await GetFormAsync("/Borrow/Correct/1");
        Assert.Contains("ย้อนกลับได้อีก", cform); Assert.Contains(">40<", cform);
        var noReason = await PostAsync("/Borrow/Correct/1", ct, new() { ["Form.Quantity"] = "10", ["Form.ClientRequestId"] = cc });
        Assert.Equal(HttpStatusCode.OK, noReason.StatusCode); Assert.Contains("ต้องระบุเหตุผล", WebUtility.HtmlDecode(await noReason.Content.ReadAsStringAsync()));
        var tooMuch = await PostAsync("/Borrow/Correct/1", ct, new() { ["Form.Quantity"] = "41", ["Form.Note"] = "ผิด", ["Form.ClientRequestId"] = cc });
        Assert.Contains("ย้อนกลับได้ไม่เกิน 40", WebUtility.HtmlDecode(await tooMuch.Content.ReadAsStringAsync()));
        Assert.Equal(2, _factory.Returns.Events.Count);
        var fix = await PostAsync("/Borrow/Correct/1", ct, new() { ["Form.Quantity"] = "15", ["Form.Note"] = "บันทึกจำนวนผิด", ["Form.ClientRequestId"] = cc });
        Assert.Equal(HttpStatusCode.Redirect, fix.StatusCode);
        Assert.Contains("/Borrow/History?receiveNo=O6900050", fix.Headers.Location!.ToString());
        Assert.Equal(3, _factory.Returns.Events.Count);
        var original = _factory.Returns.Events.Single(e => e.Id == 1); Assert.Equal(40m, original.QuantityDelta);   // never edited
        var correction = _factory.Returns.Events.Single(e => e.EventType == BorrowReturnEventType.Correction);
        Assert.Equal(-15m, correction.QuantityDelta); Assert.Equal(1, correction.CorrectsEventId); Assert.Equal("บันทึกจำนวนผิด", correction.Note);

        var after = WebUtility.HtmlDecode(await _client.GetStringAsync("/Borrow/History?receiveNo=O6900050"));
        Assert.Contains("ย้อนกลับรายการ #1", after); Assert.Contains("-15", after); Assert.Contains("ย้อนกลับรวม 15", Text(after));
        var facility = WebUtility.HtmlDecode(await _client.GetStringAsync("/Borrow/Facility/CUB001?status=all"));
        Assert.Contains("คืนแล้ว 25", Text(Regex.Match(facility, "id=\"item-900\">(.*?)</dl>", RegexOptions.Singleline).Groups[1].Value));
    }

    [Fact]
    public async Task Source_quantity_lowered_below_returned_is_flagged_for_review_not_truncated()
    {
        await PrimeAsync();
        var (_, t, c) = await GetFormAsync("/Borrow/Return/500");
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync("/Borrow/Return/500", t, new() { ["Form.Quantity"] = "5", ["Form.ClientRequestId"] = c })).StatusCode);
        // INV later edits the borrow line: QTY_ORDER 5 → 2
        var bills = _factory.Source.Bills.ToList();
        var pao = bills.Single(b => b.SourceRecordNumber == 50);
        bills[bills.IndexOf(pao)] = pao with { Items = [pao.Items[0] with { QtyOrder = 2 }] };
        _factory.Source.Bills = bills;
        try
        {
            var html = WebUtility.HtmlDecode(await _client.GetStringAsync("/Borrow/Facility/PAO001?status=all"));
            Assert.Contains("ต้องตรวจสอบ", html); Assert.Contains("INV ถูกแก้ไขหลังบันทึกคืน", html);
            Assert.Contains("ยืม 2", Text(html)); Assert.Contains("คืนแล้ว 5", Text(html));
            Assert.Single(_factory.Returns.Events);                                      // no automatic correction
            var index = WebUtility.HtmlDecode(await _client.GetStringAsync("/Borrow"));
            Assert.Contains("ต้องตรวจสอบ 1 รายการ", Text(index));
            Assert.Contains("PAO001", index);                                             // conflicts stay under the outstanding filter
            var returnPage = WebUtility.HtmlDecode(await _client.GetStringAsync("/Borrow/Return/500"));
            Assert.Contains("ข้อมูลขัดแย้งกับต้นทาง", returnPage);
        }
        finally { _factory.Source.Bills = bills.Select(b => b.SourceRecordNumber == 50 ? pao : b).ToList(); }
    }

    [Fact]
    public async Task Receipt_after_borrow_hint_is_advisory_and_its_failure_only_warns()
    {
        await PrimeAsync();
        var html = WebUtility.HtmlDecode(await _client.GetStringAsync("/Borrow/Facility/CUB001?status=all"));
        var row = Regex.Match(html, "id=\"item-1036\">(.*?)</section>", RegexOptions.Singleline).Groups[1].Value;
        Assert.Contains("มีการรับเข้าหลังยืม", row); Assert.Contains("O6900085", row); Assert.Contains("ระบบไม่ถือว่าเป็นการคืนอัตโนมัติ", row);
        Assert.Contains("คงเหลือ 250", Text(row));                                      // nothing auto-returned
        Assert.DoesNotContain("ต้องคืน", row);
        _factory.Advisory.Fail = true;
        try
        {
            var degraded = WebUtility.HtmlDecode(await _client.GetStringAsync("/Borrow/Facility/CUB001?status=all"));
            Assert.Contains("ไม่สามารถอ่านข้อมูลการรับเข้าหลังยืม", degraded);
            Assert.Contains("AMILORIDE", degraded);
        }
        finally { _factory.Advisory.Fail = false; }
    }

    [Fact]
    public async Task Health_reports_the_borrow_store_independently_and_the_full_DI_graph_resolves()
    {
        // real IAppDatabaseHealth + real BorrowScreenService graph (only the Borrow repositories are faked) — a missing
        // registration would surface here as a 500 instead of the health page
        var r = await _client.GetAsync("/Health");
        var html = WebUtility.HtmlDecode(await r.Content.ReadAsStringAsync());
        Assert.True(r.StatusCode is HttpStatusCode.OK or HttpStatusCode.ServiceUnavailable, r.StatusCode.ToString());
        Assert.Contains("ฐานข้อมูลของเว็บ (ยายืม)", html);
        Assert.Contains("สำเนายายืมเปลี่ยนล่าสุด", html);
        Assert.DoesNotContain("เกิดข้อผิดพลาดในการประมวลผลคำขอ", html);
    }

    [Fact]
    public async Task MySql_outage_renders_in_page_errors_never_the_global_error_page()
    {
        await PrimeAsync();
        _factory.Returns.Fail = true;
        try
        {
            foreach (var url in new[] { "/Borrow", "/Borrow/Facility/CUB001", "/Borrow/Return/900", "/Borrow/ReturnBill/100", "/Borrow/History", "/Borrow/Correct/1" })
            {
                var r = await _client.GetAsync(url);
                var html = WebUtility.HtmlDecode(await r.Content.ReadAsStringAsync());
                Assert.Equal(HttpStatusCode.OK, r.StatusCode);
                Assert.Contains("ไม่สามารถอ่านข้อมูลยายืมจากฐานข้อมูลของเว็บได้", html);
                Assert.DoesNotContain("เกิดข้อผิดพลาดในการประมวลผลคำขอ", html);   // global /Error page
                Assert.DoesNotContain("MySqlException", html);
            }
            // non-Borrow pages are unaffected
            Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/Reorder")).StatusCode);
        }
        finally { _factory.Returns.Fail = false; }
    }
}
