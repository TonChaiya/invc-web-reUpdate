namespace Invc.UnitTests;

/// <summary>
/// Regression guard for the shared footer: the Bootstrap-template rule `.footer { white-space: nowrap }` made the footer
/// sentence 432px wide at a 375px viewport (page-level horizontal scroll on every page). Narrow screens must allow wrapping.
/// </summary>
public class LayoutFooterOverflowTests
{
    [Fact]
    public void Layout_css_lets_the_footer_wrap_on_narrow_screens()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Invc.slnx"))) { root = root.Parent; }
        var css = File.ReadAllText(Path.Combine(root!.FullName, "src", "Invc.Web", "Pages", "Shared", "_Layout.cshtml.css"));
        // the application footer must always be allowed to wrap (the old Bootstrap-template rule forced nowrap)
        Assert.Matches(@"\.app-footer\s*\{[^}]*white-space:\s*normal", css);
        Assert.DoesNotMatch(@"\.app-footer\s*\{[^}]*white-space:\s*nowrap", css);
        var site = File.ReadAllText(Path.Combine(root!.FullName, "src", "Invc.Web", "wwwroot", "css", "site.css"));
        Assert.Matches(@"\.app-footer\s*\{[^}]*white-space:\s*normal", site);
    }
}
