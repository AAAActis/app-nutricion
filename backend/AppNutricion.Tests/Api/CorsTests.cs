using AppNutricion.Tests.Infraestructura;
using Microsoft.AspNetCore.Hosting;

namespace AppNutricion.Tests.Api;

public class CorsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string OrigenPermitido = "http://front-permitido.test";
    private const string OrigenAjeno = "http://otro-sitio.test";
    private const string HeaderCors = "Access-Control-Allow-Origin";

    private HttpClient ClienteConOrigenesPermitidos() =>
        factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Cors:AllowedOrigins:0", OrigenPermitido)).CreateClient();

    private static async Task<HttpResponseMessage> PreflightDesde(HttpClient client, string origen, string metodo = "GET")
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/health");
        request.Headers.Add("Origin", origen);
        request.Headers.Add("Access-Control-Request-Method", metodo);
        request.Headers.Add("Access-Control-Request-Headers", "authorization");
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task OrigenConfigurado_RecibeHeadersCors()
    {
        var response = await PreflightDesde(ClienteConOrigenesPermitidos(), OrigenPermitido);

        Assert.True(response.Headers.TryGetValues(HeaderCors, out var valores), $"Falta {HeaderCors}.");
        Assert.Equal(OrigenPermitido, Assert.Single(valores));
    }

    // El frontend manda el JWT en "Authorization" (no es un header simple) y usa métodos como DELETE:
    // el preflight tiene que autorizar los dos, no solo el origen.
    [Fact]
    public async Task OrigenConfigurado_PermiteHeaderAuthorizationYMetodosNoSimples()
    {
        var response = await PreflightDesde(ClienteConOrigenesPermitidos(), OrigenPermitido, "DELETE");

        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Headers", out var headers), "Falta Access-Control-Allow-Headers.");
        Assert.Contains("authorization", string.Join(",", headers), StringComparison.OrdinalIgnoreCase);
        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Methods", out var metodos), "Falta Access-Control-Allow-Methods.");
        Assert.Contains("DELETE", string.Join(",", metodos));
    }

    [Fact]
    public async Task OrigenNoConfigurado_NoRecibeHeadersCors()
    {
        var response = await PreflightDesde(ClienteConOrigenesPermitidos(), OrigenAjeno);

        Assert.False(response.Headers.Contains(HeaderCors));
    }

    [Fact]
    public async Task SinConfiguracion_SoloMismoOrigen()
    {
        var response = await PreflightDesde(factory.CreateClient(), OrigenPermitido);

        Assert.False(response.Headers.Contains(HeaderCors));
    }
}
