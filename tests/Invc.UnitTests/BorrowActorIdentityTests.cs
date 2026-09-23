using System.Security.Claims;
using System.Security.Principal;
using Invc.Core.Borrow;
using Invc.Web.Borrow;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Invc.UnitTests;

/// <summary>
/// The audit actor of every return/correction event. With IIS Windows Authentication enabled on the site (production
/// 2026-09-23) in-process hosting populates <c>HttpContext.User</c> from the IIS-authenticated Windows identity, so
/// <see cref="BorrowScreenService.CurrentActor"/> must store that name — the documented `anonymous:&lt;ip&gt;` fallback
/// is used only when no authenticated identity is present. The actor can never come from the browser/form.
/// </summary>
public class BorrowActorIdentityTests
{
    private static BorrowScreenService Service(HttpContext? context)
    {
        var accessor = new HttpContextAccessor { HttpContext = context };
        return new BorrowScreenService(null!, null!, null!, null!, accessor, NullLogger<BorrowScreenService>.Instance);
    }

    private static DefaultHttpContext Authenticated(string name)
    {
        var ctx = new DefaultHttpContext();
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, name)], "Negotiate"));   // what IIS integration sets
        ctx.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.55");
        return ctx;
    }

    [Fact]
    public void Authenticated_windows_identity_is_used_as_the_actor()
    {
        var ctx = Authenticated(@"DESKTOP-BVH8F8L\Mainam8888");
        Assert.True(ctx.User.Identity!.IsAuthenticated);
        Assert.Equal(@"DESKTOP-BVH8F8L\Mainam8888", Service(ctx).CurrentActor());
    }

    [Fact]
    public void Windows_principal_identity_name_is_used_unchanged()
    {
        var ctx = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new GenericIdentity(@"CONTOSO\nurse01", "Negotiate"))) };
        Assert.Equal(@"CONTOSO\nurse01", Service(ctx).CurrentActor());
    }

    [Fact]
    public void Unauthenticated_request_falls_back_to_the_documented_anonymous_marker()
    {
        var ctx = new DefaultHttpContext();
        ctx.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.55");
        var actor = Service(ctx).CurrentActor();
        Assert.Equal("anonymous:192.168.1.55", actor);
        Assert.StartsWith(BorrowReturnRules.AnonymousActorPrefix, actor);
    }

    [Fact]
    public void Actor_is_never_taken_from_request_input()
    {
        var ctx = Authenticated(@"DESKTOP-BVH8F8L\Mainam8888");
        ctx.Request.Headers["X-Actor"] = "attacker";
        ctx.Request.QueryString = new QueryString("?actor=attacker");
        Assert.Equal(@"DESKTOP-BVH8F8L\Mainam8888", Service(ctx).CurrentActor());
    }
}
