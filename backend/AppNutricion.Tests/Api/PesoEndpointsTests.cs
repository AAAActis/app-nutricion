using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AppNutricion.Tests.Infraestructura;
using static AppNutricion.Tests.Infraestructura.UsuarioDePrueba;

namespace AppNutricion.Tests.Api;

// Reloj fijo: 22/10/2026 02:00 UTC. Con el perfil en Argentina, "hoy" es 21/10/2026.
public class PesoEndpointsTests(ApiFactoryPerfil factory) : IClassFixture<ApiFactoryPerfil>
{
    private const string HoyEnArgentina = "2026-10-21";

    private async Task<HttpClient> ConPerfilAsync()
    {
        var (_, client) = Nuevo(factory);
        var perfil = await client.PutAsJsonAsync("/api/me/profile", new
        {
            birthDate = "1990-05-10", biologicalSex = "Female", heightCm = 165m,
            activityLevel = "Moderate", timeZoneId = "America/Argentina/Buenos_Aires",
        });
        Assert.Equal(HttpStatusCode.OK, perfil.StatusCode);
        return client;
    }

    private static Task<HttpResponseMessage> CargarAsync(HttpClient client, decimal? weightKg, string? date = null) =>
        client.PostAsJsonAsync("/api/me/metrics", new { weightKg, date });

    private static List<(string Fecha, decimal Kg)> Registros(JsonElement json) =>
        json.GetProperty("metrics").EnumerateArray()
            .Select(m => (m.GetProperty("date").GetString()!, m.GetProperty("weightKg").GetDecimal())).ToList();

    // Afirma el 200 antes de leer el cuerpo: si falla, falla por aserción.
    private static async Task<JsonElement> HistorialAsync(HttpClient client, string ruta = "/api/me/metrics")
    {
        var response = await client.GetAsync(ruta);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await JsonAsync(response);
    }

    [Fact]
    public async Task PostYGet_SinPerfil_409PidiendoElPerfil()
    {
        var (_, client) = Nuevo(factory);

        var post = await CargarAsync(client, 70m);
        var get = await client.GetAsync("/api/me/metrics");

        foreach (var response in new[] { post, get })
        {
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("Primero completá el perfil.", (await JsonAsync(response)).GetProperty("title").GetString());
        }
    }

    // Sin fecha es "hoy" en la zona del perfil: 21/10 en Argentina (en UTC ya sería 22/10).
    [Fact]
    public async Task Post_SinFecha_201ConHoyEnLaZonaDelPerfil()
    {
        var client = await ConPerfilAsync();

        var response = await CargarAsync(client, 72.5m);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await JsonAsync(response);
        Assert.Equal(HoyEnArgentina, json.GetProperty("date").GetString());
        Assert.Equal(72.5m, json.GetProperty("weightKg").GetDecimal());
    }

    // Un registro por día: el segundo del mismo día actualiza el peso (200), no duplica.
    [Fact]
    public async Task Post_MismoDiaDosVeces_ActualizaSinDuplicar()
    {
        var client = await ConPerfilAsync();
        await CargarAsync(client, 72.5m, "2026-10-10");

        var segundo = await CargarAsync(client, 71.8m, "2026-10-10");

        Assert.Equal(HttpStatusCode.OK, segundo.StatusCode);
        var registros = Registros(await HistorialAsync(client));
        Assert.Equal([("2026-10-10", 71.8m)], registros);
    }

    public static TheoryData<decimal?, string?, string> PesosInvalidos() => new()
    {
        { null, null, "weightKg" },
        { 19.9m, null, "weightKg" },
        { 400.1m, null, "weightKg" },
        { 70m, "2026-10-22", "date" },   // mañana en Argentina (aunque en UTC ya sea hoy)
        { 70m, "2024-10-20", "date" },   // más de 2 años atrás
    };

    [Theory]
    [MemberData(nameof(PesosInvalidos))]
    public async Task Post_Invalido_400ConElCampo(decimal? kg, string? fecha, string campo)
    {
        var client = await ConPerfilAsync();

        var response = await CargarAsync(client, kg, fecha);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal([campo], await CamposConErrorAsync(response));
    }

    [Theory]
    [InlineData(20, HoyEnArgentina)]
    [InlineData(400, "2024-10-21")]   // justo 2 años atrás
    public async Task Post_EnLosBordes_201(decimal kg, string fecha)
    {
        var client = await ConPerfilAsync();

        var response = await CargarAsync(client, kg, fecha);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Get_OrdenadoPorFechaYFiltradoPorRango()
    {
        var client = await ConPerfilAsync();
        await CargarAsync(client, 72m, "2026-10-05");
        await CargarAsync(client, 73m, "2026-10-01");
        await CargarAsync(client, 71m, "2026-10-10");

        var todos = Registros(await HistorialAsync(client));
        var rango = Registros(await HistorialAsync(client, "/api/me/metrics?from=2026-10-02&to=2026-10-10"));

        Assert.Equal([("2026-10-01", 73m), ("2026-10-05", 72m), ("2026-10-10", 71m)], todos);
        Assert.Equal([("2026-10-05", 72m), ("2026-10-10", 71m)], rango);
    }

    [Fact]
    public async Task Get_FromPosteriorATo_400()
    {
        var client = await ConPerfilAsync();

        var response = await client.GetAsync("/api/me/metrics?from=2026-10-10&to=2026-10-01");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(["from"], await CamposConErrorAsync(response));
    }

    // Tendencia: promedio de los últimos 7 registros contra el de los 7 anteriores.
    // Anteriores (1 al 7/10): 80,0 … 81,2 de a 0,2 → 80,6. Últimos (8 al 14/10): 79,0 … 80,2 → 79,6. Diferencia −1,0.
    // No depende del rango pedido: se calcula con los últimos 14 registros.
    [Fact]
    public async Task Get_Con14Registros_TendenciaDeLosUltimos7ContraLos7Anteriores()
    {
        var client = await ConPerfilAsync();
        for (var dia = 1; dia <= 7; dia++)
        {
            await CargarAsync(client, 80.0m + 0.2m * (dia - 1), $"2026-10-{dia:00}");
            await CargarAsync(client, 79.0m + 0.2m * (dia - 1), $"2026-10-{dia + 7:00}");
        }

        var json = await HistorialAsync(client, "/api/me/metrics?from=2026-10-13&to=2026-10-14");

        var tendencia = json.GetProperty("trend");
        Assert.Equal(79.6m, tendencia.GetProperty("lastAverageKg").GetDecimal());
        Assert.Equal(80.6m, tendencia.GetProperty("previousAverageKg").GetDecimal());
        Assert.Equal(-1.0m, tendencia.GetProperty("changeKg").GetDecimal());
        Assert.Equal(2, Registros(json).Count);
    }

    [Fact]
    public async Task Get_Con13Registros_TendenciaNull()
    {
        var client = await ConPerfilAsync();
        for (var dia = 1; dia <= 13; dia++)
            await CargarAsync(client, 80m, $"2026-10-{dia:00}");

        var json = await HistorialAsync(client);

        Assert.Equal(JsonValueKind.Null, json.GetProperty("trend").ValueKind);
        Assert.Equal(13, Registros(json).Count);
    }

    // Los pesos de otro usuario nunca se ven.
    [Fact]
    public async Task Get_SoloDevuelveLosPesosPropios()
    {
        var ana = await ConPerfilAsync();
        var beto = await ConPerfilAsync();
        await CargarAsync(ana, 60m, "2026-10-01");

        var json = await HistorialAsync(beto);

        Assert.Empty(Registros(json));
    }
}
