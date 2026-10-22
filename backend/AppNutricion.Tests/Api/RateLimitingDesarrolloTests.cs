using AppNutricion.Tests.Infraestructura;
using Microsoft.AspNetCore.Http;

namespace AppNutricion.Tests.Api;

// En Development el límite de auth es 20 (appsettings.Development.json), para no bloquear la demo ni las pruebas.
// Producción sigue con 5 (RateLimitPolicies.AuthPermitLimitDefault).
public class RateLimitingDesarrolloTests(ApiFactoryRateLimitDesarrollo factory) : IClassFixture<ApiFactoryRateLimitDesarrollo>
{
    private const string LoginJson = """{"email":"nadie@mail.com","password":"una-clave-cualquiera"}""";

    [Fact]
    public async Task Auth_EnDevelopment_20IntentosPasanYEl21Da429()
    {
        const string ip = "10.0.4.1";
        for (var i = 1; i <= 20; i++)
            Assert.Equal(StatusCodes.Status401Unauthorized, (await factory.EnviarAsync("POST", "/api/auth/login", ip, LoginJson)).Response.StatusCode);

        var intento21 = await factory.EnviarAsync("POST", "/api/auth/login", ip, LoginJson);

        Assert.Equal(StatusCodes.Status429TooManyRequests, intento21.Response.StatusCode);
    }
}
