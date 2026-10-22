using System.Security.Claims;
using AppNutricion.Presentation.Auth;
using Microsoft.AspNetCore.Http;

namespace AppNutricion.Tests.Auth;

public class CurrentUserTests
{
    private static CurrentUser ConClaims(params Claim[] claims) =>
        new(new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer")) },
        });

    [Fact]
    public void UserId_ConSubNumerico_DevuelveElId()
    {
        var actual = ConClaims(new Claim("sub", "42"));

        Assert.Equal(42, actual.UserId);
        Assert.Equal(42, actual.RequireUserId());
    }

    // Solo cuenta "sub": no se toma el id de otro claim (con MapInboundClaims = false no hay NameIdentifier).
    [Fact]
    public void UserId_ConIdEnOtroClaim_EsNull()
    {
        var actual = ConClaims(new Claim(ClaimTypes.NameIdentifier, "42"));

        Assert.Null(actual.UserId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("4 2")]
    [InlineData("99999999999")]   // no entra en int
    public void UserId_ConSubNoNumerico_EsNull(string sub)
    {
        Assert.Null(ConClaims(new Claim("sub", sub)).UserId);
    }

    [Fact]
    public void UserId_SinHttpContext_EsNull()
    {
        Assert.Null(new CurrentUser(new HttpContextAccessor()).UserId);
    }

    // Si un endpoint con [Authorize] no tiene userId, es un bug: tiene que ser un 500, nunca seguir con 0.
    [Fact]
    public void RequireUserId_SinUserId_LanzaInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() => ConClaims().RequireUserId());
        Assert.Throws<InvalidOperationException>(() => new CurrentUser(new HttpContextAccessor()).RequireUserId());
    }
}
