using System.Net;
using System.Net.Http.Json;
using AppNutricion.Tests.Infraestructura;

namespace AppNutricion.Tests.Api;

// Sin token, cada endpoint de J7/J8 responde 401 (lo da [Authorize]). Sin [Authorize] la llamada llegaría
// al controller y RequireUserId daría 500.
public class AutorizacionPerfilEndpointsTests(ApiFactoryPerfil factory) : IClassFixture<ApiFactoryPerfil>
{
    [Theory]
    [InlineData("GET", "/api/me/profile")]
    [InlineData("PUT", "/api/me/profile")]
    [InlineData("GET", "/api/me/metrics")]
    [InlineData("POST", "/api/me/metrics")]
    [InlineData("GET", "/api/me/goals")]
    [InlineData("POST", "/api/me/goals")]
    [InlineData("GET", "/api/me/targets")]
    public async Task SinToken_401(string metodo, string ruta)
    {
        var request = new HttpRequestMessage(new HttpMethod(metodo), ruta);
        if (metodo is "PUT" or "POST") request.Content = JsonContent.Create(new { });

        var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
