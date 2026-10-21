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

    private static async Task<HttpResponseMessage> PreflightDesde(HttpClient client, string origen)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/health");
        request.Headers.Add("Origin", origen);
        request.Headers.Add("Access-Control-Request-Method", "GET");
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
