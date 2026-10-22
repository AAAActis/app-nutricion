using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.Tests.Infraestructura;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using static AppNutricion.Tests.Infraestructura.UsuarioDePrueba;

namespace AppNutricion.Tests.Api;

// El access token sigue bien firmado y sin vencer después de borrar la cuenta. JwtBearer (OnTokenValidated)
// tiene que rechazarlo si el usuario del "sub" ya no existe: 401 en todos los endpoints, no solo en /api/me.
public class TokenDeCuentaBorradaTests : IClassFixture<ApiFactoryAuth>
{
    private const string Password = "una-clave-segura";
    private readonly Mock<IMealEntryManager> _mealManager = new();
    private readonly HttpClient _client;

    public TokenDeCuentaBorradaTests(ApiFactoryAuth factory)
    {
        _client = factory.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
            services.Replace(ServiceDescriptor.Scoped(_ => _mealManager.Object)))).CreateClient();
        _mealManager.Setup(m => m.ListAsync(It.IsAny<int>(), It.IsAny<DateOnly>())).ReturnsAsync([]);
    }

    private Task<HttpResponseMessage> EnviarAsync(HttpMethod metodo, string ruta, string token, object? body = null)
    {
        var request = new HttpRequestMessage(metodo, ruta);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return _client.SendAsync(request);
    }

    [Fact]
    public async Task TokenDeCuentaBorrada_EnMeYEnMeals_401SinLlegarAlManager()
    {
        var email = $"u{Guid.NewGuid():N}@mail.com";
        await _client.PostAsJsonAsync("/api/auth/register", new { email, password = Password, consentAccepted = true });
        var login = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        var token = (await JsonAsync(login)).GetProperty("accessToken").GetString()!;
        // Control: antes de borrar, el mismo token entra.
        Assert.Equal(HttpStatusCode.OK, (await EnviarAsync(HttpMethod.Get, "/api/meals?date=2026-10-21", token)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await EnviarAsync(HttpMethod.Delete, "/api/me", token, new { password = Password })).StatusCode);
        _mealManager.Invocations.Clear();

        var me = await EnviarAsync(HttpMethod.Get, "/api/me", token);
        var meals = await EnviarAsync(HttpMethod.Get, "/api/meals?date=2026-10-21", token);

        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, meals.StatusCode);
        Assert.Empty(_mealManager.Invocations);
    }
}
