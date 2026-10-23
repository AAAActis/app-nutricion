using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Tests.Infraestructura;
using Microsoft.Extensions.DependencyInjection;
using static AppNutricion.Tests.Infraestructura.UsuarioDePrueba;

namespace AppNutricion.Tests.Api;

// Ningún log (nivel Trace, todas las categorías) incluye contraseñas, hashes, tokens ni el body de auth.
// Recorre registro, login fallido y exitoso, el token en un endpoint, un token adulterado y un registro inválido.
// En Trace, MVC loguea los argumentos de la acción con su ToString(): el de un record incluye todas sus propiedades.
public class LogsSinSecretosTests(ApiFactoryLogs factory) : IClassFixture<ApiFactoryLogs>
{
    [Fact]
    public async Task FlujoDeAuth_NingunLogTieneContraseñasHashesTokensNiElBody()
    {
        var client = factory.CreateClient();
        var email = $"u{Guid.NewGuid():N}@mail.com";
        const string password = "Clave-Que-No-Se-Loguea-123";
        const string passwordIncorrecta = "Otra-Clave-Que-Tampoco-456";

        Assert.Equal(HttpStatusCode.Created,
            (await client.PostAsJsonAsync("/api/auth/register", new { email, password, consentAccepted = true })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/auth/login", new { email, password = passwordIncorrecta })).StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        var token = (await JsonAsync(login)).GetProperty("accessToken").GetString()!;
        var me = new HttpRequestMessage(HttpMethod.Get, "/api/me") { Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) } };
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(me)).StatusCode);
        var adulterado = token[..^4] + "AAAA";
        var meAdulterado = new HttpRequestMessage(HttpMethod.Get, "/api/me") { Headers = { Authorization = new AuthenticationHeaderValue("Bearer", adulterado) } };
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(meAdulterado)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/auth/register", new { email = "no-es-email", password = passwordIncorrecta, consentAccepted = true })).StatusCode);

        var hash = (await factory.Services.GetRequiredService<IUserAccountStore>().GetByEmailAsync(email))!.PasswordHash;
        var logs = string.Join("\n", factory.Logs.Lineas);
        Assert.NotEmpty(factory.Logs.Lineas);   // se capturó algo: el test no pasa por no loguear nada
        Assert.Contains("/api/auth/login", logs);   // y se capturaron los requests de auth
        foreach (var secreto in new[] { password, passwordIncorrecta, hash, token, adulterado, token.Split('.')[2] })
            Assert.DoesNotContain(secreto, logs);
        Assert.DoesNotContain("\"password\"", logs, StringComparison.OrdinalIgnoreCase);
    }
}
