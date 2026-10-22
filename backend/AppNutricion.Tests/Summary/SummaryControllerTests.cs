using System.Net;
using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.Entities.Enums;
using AppNutricion.Tests.Infraestructura;
using Moq;

namespace AppNutricion.Tests.Summary;

public class SummaryControllerTests : IClassFixture<ApiFactoryAutenticada>
{
    private const int UserId = 7;
    private static readonly DateOnly Dia = new(2026, 10, 20);

    private readonly ApiFactoryAutenticada _factory;

    public SummaryControllerTests(ApiFactoryAutenticada factory)
    {
        _factory = factory;
        _factory.SummaryManager.Reset();
    }

    public static TheoryData<string> Endpoints => new() { "/api/summary/daily" };

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task SinToken_Devuelve401(string ruta)
    {
        var response = await _factory.CrearCliente(null).GetAsync(ruta);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(_factory.SummaryManager.Invocations);
    }

    [Fact]
    public async Task Daily_ConObjetivo_UserIdDelTokenYFormaDeLaRespuesta()
    {
        _factory.SummaryManager.Setup(m => m.GetDailyAsync(UserId, Dia)).ReturnsAsync(new DailySummary(
            Dia, new(2300, 120, 100, 80), [new(MealType.Dinner, new(2300, 120, 100, 80), 3)],
            new(2000, 120, 241, 60), new(new(-300, true), new(0, false), new(141, false), new(-20, true)), new(115, 100, 41.5m, 133.3m)));

        var response = await _factory.CrearCliente(UserId).GetAsync("/api/summary/daily?date=2026-10-20");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await UsuarioDePrueba.JsonAsync(response);
        Assert.Equal("2026-10-20", json.GetProperty("date").GetString());
        Assert.Equal(2300, json.GetProperty("consumed").GetProperty("calories").GetDecimal());
        var tipo = Assert.Single(json.GetProperty("byMealType").EnumerateArray());
        Assert.Equal("Dinner", tipo.GetProperty("mealType").GetString());
        Assert.Equal(120, tipo.GetProperty("proteinG").GetDecimal());
        Assert.Equal(3, tipo.GetProperty("entries").GetInt32());
        Assert.Equal(241, json.GetProperty("targets").GetProperty("carbsG").GetDecimal());
        var kcal = json.GetProperty("remaining").GetProperty("calories");
        Assert.Equal(-300, kcal.GetProperty("value").GetDecimal());
        Assert.True(kcal.GetProperty("exceeded").GetBoolean());
        Assert.Equal(133.3m, json.GetProperty("progressPct").GetProperty("fatG").GetDecimal());
        Assert.Equal("Estimación, no es consejo médico.", json.GetProperty("disclaimer").GetString());
    }

    [Fact]
    public async Task Daily_SinObjetivoNiFecha_NullsYHoyDelManager()
    {
        _factory.SummaryManager.Setup(m => m.GetDailyAsync(UserId, null))
            .ReturnsAsync(new DailySummary(Dia, new(0, 0, 0, 0), [], null, null, null));

        var response = await _factory.CrearCliente(UserId).GetAsync("/api/summary/daily");

        var json = await UsuarioDePrueba.JsonAsync(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(json.GetProperty("byMealType").EnumerateArray());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, json.GetProperty("targets").ValueKind);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, json.GetProperty("remaining").ValueKind);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, json.GetProperty("progressPct").ValueKind);
    }

    [Fact]
    public async Task Daily_FechaFueraDeRango_Devuelve400ConElCampoDate()
    {
        _factory.SummaryManager.Setup(m => m.GetDailyAsync(UserId, It.IsAny<DateOnly?>()))
            .ThrowsAsync(new ArgumentException("La fecha no puede ser futura ni de hace más de 2 años.", "date"));

        var response = await _factory.CrearCliente(UserId).GetAsync("/api/summary/daily?date=2030-01-01");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(["date"], await UsuarioDePrueba.CamposConErrorAsync(response));
    }
}
