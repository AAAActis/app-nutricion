using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AppNutricion.Tests.Infraestructura;
using static AppNutricion.Tests.Infraestructura.UsuarioDePrueba;

namespace AppNutricion.Tests.Api;

// Reloj fijo: 22/10/2026 02:00 UTC = 21/10/2026 en Argentina.
// Perfil base: hombre nacido el 20/10/1996 (30 años), 180 cm, actividad moderada, 80 kg.
// Mismos números que NutritionCalculatorCalculateTests: gasto total 2759; LoseFat 0,5 → 2209 kcal,
// proteína 144, grasa 61,4, carbohidratos 270,2; IMC 24,7; 5 kg / 0,5 = 10 semanas → 21/10 + 70 días = 30/12/2026.
public class ObjetivosEndpointsTests(ApiFactoryPerfil factory) : IClassFixture<ApiFactoryPerfil>
{
    private const string Hoy = "2026-10-21";

    private async Task<HttpClient> UsuarioAsync(string birthDate = "1996-10-20", string sexo = "Male", decimal altura = 180m,
        string actividad = "Moderate", decimal? peso = 80m)
    {
        var (_, client) = Nuevo(factory);
        var perfil = await client.PutAsJsonAsync("/api/me/profile", new
        {
            birthDate, biologicalSex = sexo, heightCm = altura, activityLevel = actividad, timeZoneId = "America/Argentina/Buenos_Aires",
        });
        Assert.Equal(HttpStatusCode.OK, perfil.StatusCode);
        if (peso is not null)
            Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/me/metrics", new { weightKg = peso, date = Hoy })).StatusCode);
        return client;
    }

    private static Task<HttpResponseMessage> CrearAsync(HttpClient client, string? goalType, decimal? weeklyRateKg, decimal? targetWeightKg = null) =>
        client.PostAsJsonAsync("/api/me/goals", new { goalType, weeklyRateKg, targetWeightKg });

    private static async Task<JsonElement> TargetsAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/me/targets");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await JsonAsync(response);
    }

    // Afirma el 201 antes de leer el cuerpo: si falla, falla por aserción.
    private static async Task<JsonElement> CreadoAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await JsonAsync(response);
    }

    private static string[] CodigosDeAviso(JsonElement targets) =>
        targets.GetProperty("warnings").EnumerateArray().Select(w => w.GetProperty("code").GetString()!).ToArray();

    [Fact]
    public async Task Post_SinPerfil_409()
    {
        var (_, client) = Nuevo(factory);

        var response = await CrearAsync(client, "Maintain", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Primero completá el perfil.", (await JsonAsync(response)).GetProperty("title").GetString());
    }

    [Fact]
    public async Task Post_SinPeso_409()
    {
        var client = await UsuarioAsync(peso: null);

        var response = await CrearAsync(client, "Maintain", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Primero registrá tu peso.", (await JsonAsync(response)).GetProperty("title").GetString());
    }

    [Fact]
    public async Task Post_LoseFat_201ConTargetsCalculadosYGetTargetsDevuelveLoMismo()
    {
        var client = await UsuarioAsync();

        var response = await CrearAsync(client, "LoseFat", 0.5m, 75m);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/api/me/targets", response.Headers.Location?.OriginalString);
        foreach (var t in new[] { await JsonAsync(response), await TargetsAsync(client) })
        {
            Assert.Equal("LoseFat", t.GetProperty("goalType").GetString());
            Assert.Equal(2209m, t.GetProperty("calories").GetDecimal());
            Assert.Equal(144.0m, t.GetProperty("proteinG").GetDecimal());
            Assert.Equal(270.2m, t.GetProperty("carbsG").GetDecimal());
            Assert.Equal(61.4m, t.GetProperty("fatG").GetDecimal());
            Assert.Equal(24.7m, t.GetProperty("bmi").GetDecimal());
            Assert.Equal("Normal", t.GetProperty("bmiCategory").GetString());
            Assert.Equal(10.0m, t.GetProperty("projection").GetProperty("weeks").GetDecimal());
            Assert.Equal("2026-12-30", t.GetProperty("projection").GetProperty("estimatedDate").GetString());
            Assert.Empty(CodigosDeAviso(t));
            Assert.Equal("Estimación, no es consejo médico.", t.GetProperty("disclaimer").GetString());
        }
    }

    // Los targets quedan congelados: un peso nuevo no cambia las kcal, pero el IMC y la proyección sí (son del último peso).
    [Fact]
    public async Task GetTargets_DespuesDeUnPesoNuevo_KcalCongeladasEImcActual()
    {
        var client = await UsuarioAsync();
        await CrearAsync(client, "LoseFat", 0.5m, 75m);

        await client.PostAsJsonAsync("/api/me/metrics", new { weightKg = 77m, date = Hoy });   // corrige el peso de hoy
        var t = await TargetsAsync(client);

        Assert.Equal(2209m, t.GetProperty("calories").GetDecimal());
        Assert.Equal(144.0m, t.GetProperty("proteinG").GetDecimal());
        Assert.Equal(23.8m, t.GetProperty("bmi").GetDecimal());                        // 77 / 1,8² = 23,77
        Assert.Equal(4.0m, t.GetProperty("projection").GetProperty("weeks").GetDecimal());   // 2 kg / 0,5
    }

    // Ritmo pedido 1 kg; el máximo con 80 kg es 0,8. Se aplica y se guarda 0,8; el aviso sale solo en el POST.
    [Fact]
    public async Task Post_RitmoPorEncimaDelMaximo_GuardaElAplicadoYAvisaSoloEnElPost()
    {
        var client = await UsuarioAsync();

        var t = await CreadoAsync(await CrearAsync(client, "LoseFat", 1.0m));
        var targets = await TargetsAsync(client);
        var goals = await JsonAsync(await client.GetAsync("/api/me/goals"));

        Assert.Equal(0.8m, t.GetProperty("weeklyRateKg").GetDecimal());
        Assert.Equal(0.8m, t.GetProperty("appliedWeeklyRateKg").GetDecimal());
        Assert.Equal(1879m, t.GetProperty("calories").GetDecimal());
        Assert.Equal(["WeeklyRateCapped"], CodigosDeAviso(t));
        Assert.Empty(CodigosDeAviso(targets));
        Assert.Equal(0.8m, goals[0].GetProperty("weeklyRateKg").GetDecimal());
    }

    // LoseFat 0,8 con 80 kg (justo el máximo). Después el peso baja a 70: el máximo sería 0,7, pero GET /targets
    // proyecta con el 0,8 guardado, sin recortar ni avisar. 5 kg / 0,8 = 6,25 semanas → 44 días → 04/12/2026.
    [Fact]
    public async Task GetTargets_ConMenosPeso_ProyectaConElRitmoGuardadoSinRecortar()
    {
        var client = await UsuarioAsync();
        Assert.Equal(HttpStatusCode.Created, (await CrearAsync(client, "LoseFat", 0.8m, 65m)).StatusCode);

        await client.PostAsJsonAsync("/api/me/metrics", new { weightKg = 70m, date = Hoy });   // corrige el peso de hoy
        var t = await TargetsAsync(client);

        Assert.Equal(0.8m, t.GetProperty("appliedWeeklyRateKg").GetDecimal());
        Assert.Empty(CodigosDeAviso(t));
        Assert.Equal(6.3m, t.GetProperty("projection").GetProperty("weeks").GetDecimal());
        Assert.Equal("2026-12-04", t.GetProperty("projection").GetProperty("estimatedDate").GetString());
    }

    // Mujer de 25 años, 165 cm, 60 kg, sedentaria, LoseFat 0,5 → 1064 kcal: se usa el piso de 1200 y se avisa.
    [Fact]
    public async Task Post_PorDebajoDelPiso_UsaElPisoYAvisa()
    {
        var client = await UsuarioAsync(birthDate: "2001-10-20", sexo: "Female", altura: 165m, actividad: "Sedentary", peso: 60m);

        var t = await CreadoAsync(await CrearAsync(client, "LoseFat", 0.5m));

        Assert.Equal(1200m, t.GetProperty("calories").GetDecimal());
        Assert.Equal(["CalorieFloorApplied"], CodigosDeAviso(t));
        Assert.False(string.IsNullOrWhiteSpace(t.GetProperty("warnings")[0].GetProperty("message").GetString()));
    }

    // Un solo objetivo activo: el nuevo desactiva al anterior. El historial va del más nuevo al más viejo.
    [Fact]
    public async Task Post_SegundoObjetivo_DesactivaElAnteriorYQuedaEnElHistorial()
    {
        var client = await UsuarioAsync();
        await CrearAsync(client, "LoseFat", 0.5m, 75m);

        var segundo = await CrearAsync(client, "Maintain", null);
        var historial = await JsonAsync(await client.GetAsync("/api/me/goals"));

        Assert.Equal(HttpStatusCode.Created, segundo.StatusCode);
        var goals = historial.EnumerateArray().ToList();
        Assert.Equal(2, goals.Count);
        Assert.Equal(("Maintain", true), (goals[0].GetProperty("goalType").GetString(), goals[0].GetProperty("isActive").GetBoolean()));
        Assert.Equal(("LoseFat", false), (goals[1].GetProperty("goalType").GetString(), goals[1].GetProperty("isActive").GetBoolean()));
        Assert.Equal(2209m, goals[1].GetProperty("targetCalories").GetDecimal());
        Assert.Equal(2759m, (await TargetsAsync(client)).GetProperty("calories").GetDecimal());
    }

    [Fact]
    public async Task GetTargets_SinObjetivoActivo_404()
    {
        var client = await UsuarioAsync();

        var response = await client.GetAsync("/api/me/targets");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetTargetsYGoals_DeOtroUsuario_NoSeVen()
    {
        var ana = await UsuarioAsync();
        var beto = await UsuarioAsync();
        Assert.Equal(HttpStatusCode.Created, (await CrearAsync(ana, "Maintain", null)).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await beto.GetAsync("/api/me/targets")).StatusCode);
        var goals = await beto.GetAsync("/api/me/goals");
        Assert.Equal(HttpStatusCode.OK, goals.StatusCode);
        Assert.Empty((await JsonAsync(goals)).EnumerateArray());
    }

    // Peso actual: 80 kg.
    public static TheoryData<string?, decimal?, decimal?, string> ObjetivosIncoherentes() => new()
    {
        { null, 0.5m, null, "goalType" },
        { "Bulk", 0.5m, null, "goalType" },
        { "0", 0.5m, null, "goalType" },
        { "LoseFat", null, null, "weeklyRateKg" },
        { "LoseFat", 0m, null, "weeklyRateKg" },
        { "GainMuscle", -0.25m, null, "weeklyRateKg" },
        { "Maintain", 0.5m, null, "weeklyRateKg" },
        { "LoseFat", 0.5m, 85m, "targetWeightKg" },      // bajar hasta un peso mayor al actual
        { "LoseFat", 0.5m, 80m, "targetWeightKg" },      // igual al actual
        { "GainMuscle", 0.25m, 75m, "targetWeightKg" },  // subir hasta un peso menor
        { "Maintain", null, 80m, "targetWeightKg" },     // mantener no lleva meta
        { "LoseFat", 0.5m, 19m, "targetWeightKg" },      // fuera de 20 a 400
    };

    [Theory]
    [MemberData(nameof(ObjetivosIncoherentes))]
    public async Task Post_Incoherente_400ConElCampoYNoTocaElActivo(string? goalType, decimal? ritmo, decimal? meta, string campo)
    {
        var client = await UsuarioAsync();
        Assert.Equal(HttpStatusCode.Created, (await CrearAsync(client, "Maintain", null)).StatusCode);

        var response = await CrearAsync(client, goalType, ritmo, meta);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal([campo], await CamposConErrorAsync(response));
        Assert.Equal("Maintain", (await TargetsAsync(client)).GetProperty("goalType").GetString());
    }

    [Theory]
    [InlineData("Maintain", null)]
    [InlineData("Maintain", 0)]
    [InlineData("maintain", null)]
    public async Task Post_MaintainSinRitmoOConCero_201(string goalType, int? ritmo)
    {
        var client = await UsuarioAsync();

        var response = await CrearAsync(client, goalType, ritmo);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // 17 años en Argentina (cumple 18 el 22/10). Bajar de peso no se permite a menores; mantener sí.
    [Fact]
    public async Task Post_MenorDe18_LoseFat400YMaintain201()
    {
        var client = await UsuarioAsync(birthDate: "2008-10-22");

        var bajar = await CrearAsync(client, "LoseFat", 0.25m);
        var mantener = await CrearAsync(client, "Maintain", null);

        Assert.Equal(HttpStatusCode.BadRequest, bajar.StatusCode);
        Assert.Equal(["goalType"], await CamposConErrorAsync(bajar));
        Assert.Equal(HttpStatusCode.Created, mantener.StatusCode);
    }
}
