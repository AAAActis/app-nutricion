using System.Text.Json;
using AppNutricion.Tests.Infraestructura;
using Microsoft.AspNetCore.Http;

namespace AppNutricion.Tests.Api;

// Límites por defecto: 5 intentos de login/registro por minuto y por IP; 20 mensajes de chat por minuto y por usuario.
// El rate limiter usa el reloj real: cada test usa IPs propias para no compartir ventana con otro test de la clase.
public class RateLimitingTests(ApiFactoryRateLimit factory) : IClassFixture<ApiFactoryRateLimit>
{
    private const string LoginJson = """{"email":"nadie@mail.com","password":"una-clave-cualquiera"}""";
    private const string RegistroInvalidoJson = """{"email":"no-es-email","password":"x","consentAccepted":false}""";

    // El cuerpo de la respuesta de TestServer se lee desde el principio y no se puede rebobinar.
    private static async Task<JsonElement> JsonAsync(HttpContext context) =>
        (await JsonDocument.ParseAsync(context.Response.Body)).RootElement;

    [Theory]
    [InlineData("/api/auth/login", LoginJson, "10.0.1.1")]
    [InlineData("/api/auth/register", RegistroInvalidoJson, "10.0.1.2")]
    public async Task Auth_SextoIntentoDesdeLaMismaIp_429ConRetryAfter(string ruta, string json, string ip)
    {
        for (var i = 1; i <= 5; i++)
            Assert.NotEqual(StatusCodes.Status429TooManyRequests, (await factory.EnviarAsync("POST", ruta, ip, json)).Response.StatusCode);

        var sexto = await factory.EnviarAsync("POST", ruta, ip, json);

        Assert.Equal(StatusCodes.Status429TooManyRequests, sexto.Response.StatusCode);
        Assert.True(int.TryParse(sexto.Response.Headers.RetryAfter.ToString(), out var segundos), "Falta Retry-After en segundos.");
        Assert.InRange(segundos, 1, 60);
        Assert.Equal("application/problem+json", sexto.Response.ContentType?.Split(';')[0]);
        Assert.Equal(429, (await JsonAsync(sexto)).GetProperty("status").GetInt32());
    }

    // Login y registro comparten la ventana de la IP: 3 logins + 2 registros agotan los 5.
    [Fact]
    public async Task Auth_LoginYRegistroCompartenLaVentanaDeLaIp()
    {
        const string ip = "10.0.1.3";
        for (var i = 1; i <= 3; i++) await factory.EnviarAsync("POST", "/api/auth/login", ip, LoginJson);
        for (var i = 1; i <= 2; i++) await factory.EnviarAsync("POST", "/api/auth/register", ip, RegistroInvalidoJson);

        var sexto = await factory.EnviarAsync("POST", "/api/auth/login", ip, LoginJson);

        Assert.Equal(StatusCodes.Status429TooManyRequests, sexto.Response.StatusCode);
    }

    [Fact]
    public async Task Auth_OtraIp_NoQuedaAfectada()
    {
        for (var i = 1; i <= 6; i++) await factory.EnviarAsync("POST", "/api/auth/login", "10.0.1.4", LoginJson);

        var otraIp = await factory.EnviarAsync("POST", "/api/auth/login", "10.0.1.5", LoginJson);

        Assert.Equal(StatusCodes.Status401Unauthorized, otraIp.Response.StatusCode);
    }

    // Usuario real (registro + login) desde una IP propia, que no gasta la ventana de auth de otro test.
    // Un token armado a mano para un usuario inexistente no serviría cuando la API rechace los de cuentas borradas (J5).
    private async Task<string> UsuarioConTokenAsync(string ip)
    {
        var json = $$"""{"email":"u{{Guid.NewGuid():N}}@mail.com","password":"una-clave-segura","consentAccepted":true}""";
        Assert.Equal(StatusCodes.Status201Created, (await factory.EnviarAsync("POST", "/api/auth/register", ip, json)).Response.StatusCode);
        var login = await factory.EnviarAsync("POST", "/api/auth/login", ip, json);
        Assert.Equal(StatusCodes.Status200OK, login.Response.StatusCode);
        return (await JsonAsync(login)).GetProperty("accessToken").GetString()!;
    }

    // La política del chat existe y limita, aunque todavía no se aplica a ningún endpoint real.
    [Fact]
    public async Task Chat_Mensaje21DelMismoUsuario_429()
    {
        var token = await UsuarioConTokenAsync("10.0.3.1");
        for (var i = 1; i <= 20; i++)
            Assert.Equal(StatusCodes.Status200OK, (await factory.EnviarAsync("POST", "/test/chat", "10.0.2.1", token: token)).Response.StatusCode);

        var mensaje21 = await factory.EnviarAsync("POST", "/test/chat", "10.0.2.1", token: token);

        Assert.Equal(StatusCodes.Status429TooManyRequests, mensaje21.Response.StatusCode);
    }

    // Por usuario, no por IP: otro usuario desde la misma IP no queda afectado.
    [Fact]
    public async Task Chat_OtroUsuarioDesdeLaMismaIp_NoQuedaAfectado()
    {
        var token = await UsuarioConTokenAsync("10.0.3.2");
        var otroToken = await UsuarioConTokenAsync("10.0.3.3");
        for (var i = 1; i <= 21; i++) await factory.EnviarAsync("POST", "/test/chat", "10.0.2.2", token: token);

        var otro = await factory.EnviarAsync("POST", "/test/chat", "10.0.2.2", token: otroToken);

        Assert.Equal(StatusCodes.Status200OK, otro.Response.StatusCode);
    }
}
