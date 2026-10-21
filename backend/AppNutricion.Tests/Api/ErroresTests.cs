using System.Net;
using System.Text.Json;
using AppNutricion.Tests.Infraestructura;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace AppNutricion.Tests.Api;

public class ErroresTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string RutaQueExplota = "/_test/explota";
    private const string DetalleInterno = "detalle-interno-que-no-debe-salir";

    [Fact]
    public async Task ExcepcionNoManejada_DevuelveProblemDetails500SinDetallesInternos()
    {
        var client = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<IStartupFilter, RutaQueExplotaFilter>())).CreateClient();

        var response = await client.GetAsync(RutaQueExplota);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(body);
        Assert.Equal(500, json.RootElement.GetProperty("status").GetInt32());
        Assert.DoesNotContain(DetalleInterno, body);
        Assert.DoesNotContain(nameof(InvalidOperationException), body);
    }

    // Agrega una ruta que lanza una excepción DESPUÉS de todo el pipeline de Program.cs,
    // así la excepción sube a través del manejador global de errores de la app.
    // El primer middleware imita a Kestrel: si nadie maneja la excepción, 500 sin cuerpo.
    // (TestServer, a diferencia de Kestrel, se la tiraría al cliente del test.)
    private sealed class RutaQueExplotaFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, siguiente) =>
            {
                try
                {
                    await siguiente(context);
                }
                catch
                {
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                }
            });
            next(app);
            app.Map(RutaQueExplota, branch => branch.Run(_ => throw new InvalidOperationException(DetalleInterno)));
        };
    }
}
