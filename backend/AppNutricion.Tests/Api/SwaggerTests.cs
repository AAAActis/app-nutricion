using System.Net;
using System.Text.Json;
using AppNutricion.Tests.Infraestructura;

namespace AppNutricion.Tests.Api;

public class SwaggerTests(ApiFactoryDesarrollo factory) : IClassFixture<ApiFactoryDesarrollo>
{
    [Fact]
    public async Task EnDesarrollo_SwaggerUiResponde()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task EnDesarrollo_DocumentoOpenApiDeclaraEsquemaBearer()
    {
        var client = factory.CreateClient();

        var body = await client.GetStringAsync("/openapi/v1.json");

        using var json = JsonDocument.Parse(body);
        Assert.True(json.RootElement.TryGetProperty("components", out var components), "El documento no tiene 'components'.");
        Assert.True(components.TryGetProperty("securitySchemes", out var esquemas), "El documento no declara 'securitySchemes'.");
        var bearer = esquemas.GetProperty("Bearer");
        Assert.Equal("http", bearer.GetProperty("type").GetString());
        Assert.Equal("bearer", bearer.GetProperty("scheme").GetString());
    }
}

public class SwaggerFueraDeDesarrolloTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Theory]
    [InlineData("/swagger/index.html")]
    [InlineData("/openapi/v1.json")]
    public async Task FueraDeDesarrollo_SwaggerYOpenApiNoSeExponen(string ruta)
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync(ruta);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
