using System.Net;
using System.Net.Http.Json;
using AppNutricion.Tests.Infraestructura;
using static AppNutricion.Tests.Infraestructura.UsuarioDePrueba;

namespace AppNutricion.Tests.Api;

// Reloj fijo: 22/10/2026 02:00 UTC = 21/10/2026 23:00 en Argentina.
public class PerfilEndpointsTests(ApiFactoryPerfil factory) : IClassFixture<ApiFactoryPerfil>
{
    private const string ZonaArgentina = "America/Argentina/Buenos_Aires";

    private static object Perfil(string? birthDate = "1990-05-10", string? biologicalSex = "Female", decimal? heightCm = 165m,
        string? activityLevel = "Moderate", string? timeZoneId = ZonaArgentina) =>
        new { birthDate, biologicalSex, heightCm, activityLevel, timeZoneId };

    [Fact]
    public async Task GetProfile_SinToken_401()
    {
        var response = await factory.CreateClient().GetAsync("/api/me/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetProfile_SinPerfil_404()
    {
        var (_, client) = Nuevo(factory);

        var response = await client.GetAsync("/api/me/profile");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task PutProfile_Valido_GuardaYGetLoDevuelve()
    {
        var (_, client) = Nuevo(factory);

        var put = await client.PutAsJsonAsync("/api/me/profile", Perfil());
        var get = await client.GetAsync("/api/me/profile");

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var json = await JsonAsync(get);
        Assert.Equal("1990-05-10", json.GetProperty("birthDate").GetString());
        Assert.Equal("Female", json.GetProperty("biologicalSex").GetString());
        Assert.Equal(165m, json.GetProperty("heightCm").GetDecimal());
        Assert.Equal("Moderate", json.GetProperty("activityLevel").GetString());
        Assert.Equal(ZonaArgentina, json.GetProperty("timeZoneId").GetString());
        Assert.Equal(ApiFactoryPerfil.Ahora.UtcDateTime, json.GetProperty("updatedAt").GetDateTime().ToUniversalTime());
        Assert.EndsWith("Z", json.GetProperty("updatedAt").GetString());
    }

    // 1 a 1 con User: el segundo PUT actualiza, no crea otro.
    [Fact]
    public async Task PutProfile_DosVeces_ActualizaElMismo()
    {
        var (_, client) = Nuevo(factory);
        await client.PutAsJsonAsync("/api/me/profile", Perfil());

        var put = await client.PutAsJsonAsync("/api/me/profile", Perfil(heightCm: 170m, activityLevel: "Active"));

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var json = await JsonAsync(await client.GetAsync("/api/me/profile"));
        Assert.Equal(170m, json.GetProperty("heightCm").GetDecimal());
        Assert.Equal("Active", json.GetProperty("activityLevel").GetString());
    }

    // El perfil de otro usuario nunca se ve.
    [Fact]
    public async Task GetProfile_DeOtroUsuario_404()
    {
        var (_, ana) = Nuevo(factory);
        var (_, beto) = Nuevo(factory);
        await ana.PutAsJsonAsync("/api/me/profile", Perfil());

        var response = await beto.GetAsync("/api/me/profile");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public static TheoryData<object, string> PerfilesInvalidos() => new()
    {
        { Perfil(birthDate: null), "birthDate" },
        // En Argentina hoy es 21/10/2026: nacido el 22/10/2012 tiene 13 (en UTC ya tendría 14).
        { Perfil(birthDate: "2012-10-22"), "birthDate" },
        { Perfil(birthDate: "1925-10-21"), "birthDate" },   // 101 años
        { Perfil(biologicalSex: null), "biologicalSex" },
        { Perfil(biologicalSex: "Otro"), "biologicalSex" },
        { Perfil(biologicalSex: "1"), "biologicalSex" },     // número en texto: no vale
        { Perfil(heightCm: null), "heightCm" },
        { Perfil(heightCm: 99.9m), "heightCm" },
        { Perfil(heightCm: 250.1m), "heightCm" },
        { Perfil(activityLevel: null), "activityLevel" },
        { Perfil(activityLevel: "Extreme"), "activityLevel" },
        { Perfil(activityLevel: "4"), "activityLevel" },
        { Perfil(timeZoneId: null), "timeZoneId" },
        { Perfil(timeZoneId: "Marte/Olympus_Mons"), "timeZoneId" },
        { Perfil(timeZoneId: "Argentina Standard Time"), "timeZoneId" },   // id de Windows, no IANA
    };

    [Theory]
    [MemberData(nameof(PerfilesInvalidos))]
    public async Task PutProfile_Invalido_400ConElCampo(object perfil, string campo)
    {
        var (_, client) = Nuevo(factory);

        var response = await client.PutAsJsonAsync("/api/me/profile", perfil);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal([campo], await CamposConErrorAsync(response));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/me/profile")).StatusCode);
    }

    public static TheoryData<object> PerfilesEnLosBordes() => new()
    {
        Perfil(birthDate: "2012-10-21"),   // 14 años justos en Argentina
        Perfil(birthDate: "1925-10-22"),   // 100 años
        Perfil(heightCm: 100m),
        Perfil(heightCm: 250m),
        Perfil(biologicalSex: "male", activityLevel: "veryactive"),   // sin distinguir mayúsculas
        Perfil(timeZoneId: "UTC"),
    };

    [Theory]
    [MemberData(nameof(PerfilesEnLosBordes))]
    public async Task PutProfile_EnLosBordes_200(object perfil)
    {
        var (_, client) = Nuevo(factory);

        var response = await client.PutAsJsonAsync("/api/me/profile", perfil);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
