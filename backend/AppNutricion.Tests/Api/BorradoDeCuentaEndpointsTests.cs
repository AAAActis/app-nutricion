using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AppNutricion.Tests.Infraestructura;
using static AppNutricion.Tests.Infraestructura.UsuarioDePrueba;

namespace AppNutricion.Tests.Api;

// DELETE /api/me con JwtBearer real y usuarios en memoria: hace falta el token y la contraseña actual.
public class BorradoDeCuentaEndpointsTests(ApiFactoryAuth factory) : IClassFixture<ApiFactoryAuth>
{
    private const string Password = "una-clave-segura";
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<(string Email, string Token)> RegistrarYLoguearAsync()
    {
        var email = $"u{Guid.NewGuid():N}@mail.com";
        Assert.Equal(HttpStatusCode.Created,
            (await _client.PostAsJsonAsync("/api/auth/register", new { email, password = Password, consentAccepted = true })).StatusCode);
        var login = await LoginAsync(email);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return (email, (await JsonAsync(login)).GetProperty("accessToken").GetString()!);
    }

    private Task<HttpResponseMessage> LoginAsync(string email) =>
        _client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });

    private Task<HttpResponseMessage> BorrarAsync(string? token, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/me") { Content = JsonContent.Create(body) };
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    [Fact]
    public async Task Delete_ContraseñaCorrecta_204YLaCuentaYaNoExiste()
    {
        var (email, token) = await RegistrarYLoguearAsync();

        var response = await BorrarAsync(token, new { password = Password });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(email)).StatusCode);
    }

    // Contraseña incorrecta, vacía, ausente o más larga que el máximo: 401 y la cuenta sigue.
    [Theory]
    [InlineData("otra-clave-cualquiera")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Delete_ContraseñaQueNoCoincide_401YLaCuentaSigue(string? password)
    {
        var (email, token) = await RegistrarYLoguearAsync();

        var response = await BorrarAsync(token, new { password });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(email)).StatusCode);
    }

    [Fact]
    public async Task Delete_ContraseñaDeMasDe128_401SinHashear()
    {
        var (email, token) = await RegistrarYLoguearAsync();

        var response = await BorrarAsync(token, new { password = new string('a', 129) });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(email)).StatusCode);
    }

    [Fact]
    public async Task Delete_SinToken_401()
    {
        var response = await BorrarAsync(null, new { password = Password });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
