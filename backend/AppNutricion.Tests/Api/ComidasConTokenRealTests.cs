using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.Tests.Infraestructura;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Moq;
using static AppNutricion.Tests.Infraestructura.UsuarioDePrueba;

namespace AppNutricion.Tests.Api;

// Punta a punta con JwtBearer real (ApiFactoryAuth): el token del login tiene que llegar a /api/meals.
// Solo IMealEntryManager es un mock, para no depender de Postgres.
public class ComidasConTokenRealTests : IClassFixture<ApiFactoryAuth>
{
    private readonly Mock<IMealEntryManager> _mealManager = new();
    private readonly HttpClient _client;

    public ComidasConTokenRealTests(ApiFactoryAuth factory)
    {
        _client = factory.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
            services.Replace(ServiceDescriptor.Scoped(_ => _mealManager.Object)))).CreateClient();
    }

    private Task<HttpResponseMessage> GetMealsAsync(string token, DateOnly dia)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/meals?date={dia:yyyy-MM-dd}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    [Fact]
    public async Task RegisterLoginYGetMeals_ConElTokenDelLogin_200ConElIdDelUsuarioRegistrado()
    {
        var email = $"u{Guid.NewGuid():N}@mail.com";
        var registro = await _client.PostAsJsonAsync("/api/auth/register",
            new { email, password = "una-clave-segura", consentAccepted = true });
        Assert.Equal(HttpStatusCode.Created, registro.StatusCode);
        var id = (await JsonAsync(registro)).GetProperty("id").GetInt32();
        var login = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = "una-clave-segura" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await JsonAsync(login)).GetProperty("accessToken").GetString()!;
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        _mealManager.Setup(m => m.ListAsync(id, hoy)).ReturnsAsync([]);

        var response = await GetMealsAsync(token, hoy);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _mealManager.Verify(m => m.ListAsync(id, hoy), Times.Once);
    }

    [Fact]
    public async Task GetMeals_TokenFirmadoConOtraClave_401SinLlegarAlManager()
    {
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, "1")]),
            Issuer = "AppNutricion.Api",
            Audience = "AppNutricion.Clientes",
            Expires = DateTime.UtcNow.AddMinutes(10),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes("otra-clave-que-no-es-la-de-la-api-32b!!")),
                SecurityAlgorithms.HmacSha256),
        });

        var response = await GetMealsAsync(token, DateOnly.FromDateTime(DateTime.UtcNow));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(_mealManager.Invocations);
    }
}
