using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AppNutricion.Tests.Infraestructura;

// Reemplaza al JWT en los tests: autentica solo si viene X-Test-UserId y pone ese valor en "sub",
// igual que JwtBearer con MapInboundClaims = false. X-Test-NameIdentifier autentica con NameIdentifier
// y sin "sub": para probar que los controllers no leen ese claim.
public class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string Esquema = "Test";
    public const string Header = "X-Test-UserId";
    public const string HeaderNameIdentifier = "X-Test-NameIdentifier";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        Claim claim;
        if (Request.Headers.TryGetValue(Header, out var userId))
            claim = new Claim("sub", userId.ToString());
        else if (Request.Headers.TryGetValue(HeaderNameIdentifier, out var nameIdentifier))
            claim = new Claim(ClaimTypes.NameIdentifier, nameIdentifier.ToString());
        else
            return Task.FromResult(AuthenticateResult.NoResult());

        var identity = new ClaimsIdentity([claim], Esquema);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Esquema)));
    }

    // Como JwtBearer: el challenge manda WWW-Authenticate. Así un test ve que el 401 lo pidió [Authorize].
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = Esquema;
        return Task.CompletedTask;
    }
}
