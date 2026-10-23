using System.Net;
using System.Net.Http.Json;
using AppNutricion.BusinessLogic.Exceptions;
using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;
using AppNutricion.Tests.Infraestructura;
using Moq;
using static AppNutricion.Tests.Infraestructura.UsuarioDePrueba;

namespace AppNutricion.Tests.Foods;

public class AlimentosPersonalizadosControllerTests : IClassFixture<ApiFactoryAutenticada>
{
    private const int UserId = 7;

    private readonly ApiFactoryAutenticada _factory;
    private readonly Food _torta = new()
    {
        Id = 9, Name = "Torta", Source = FoodSource.Custom, CreatedByUserId = UserId,
        CaloriesPer100g = 300, ProteinPer100g = 5, CarbsPer100g = 40, FatPer100g = 12, PortionName = "porción", PortionWeightGrams = 80,
    };
    private static readonly CustomFoodData Esperado = new("Torta", "Casera", 300, 5, 40, 12, "porción", 80);

    public AlimentosPersonalizadosControllerTests(ApiFactoryAutenticada factory)
    {
        _factory = factory;
        _factory.FoodManager.Reset();
    }

    private static object Cuerpo(object? userId = null) => new
    {
        name = "Torta", brand = "Casera", caloriesPer100g = 300, proteinPer100g = 5, carbsPer100g = 40, fatPer100g = 12,
        portionName = "porción", portionWeightGrams = 80, userId, source = "Argenfoods", createdByUserId = 99,
    };

    public static TheoryData<string, string> Endpoints => new()
    {
        { "POST", "/api/foods/custom" },
        { "GET", "/api/foods/custom" },
        { "GET", "/api/foods/custom/9" },
        { "PUT", "/api/foods/custom/9" },
        { "DELETE", "/api/foods/custom/9" },
    };

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task SinHeader_Devuelve401(string metodo, string ruta)
    {
        var request = new HttpRequestMessage(new HttpMethod(metodo), ruta) { Content = metodo is "POST" or "PUT" ? JsonContent.Create(Cuerpo()) : null };

        var response = await _factory.CrearCliente(null).SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(TestAuthHandler.Esquema, response.Headers.WwwAuthenticate.ToString());
        Assert.Empty(_factory.FoodManager.Invocations);
    }

    [Fact]
    public async Task Post_UsaElUserIdDelClaimYIgnoraDueñoYFuenteDelBody_Devuelve201()
    {
        _factory.FoodManager.Setup(m => m.CreateCustomAsync(UserId, Esperado)).ReturnsAsync(_torta);

        var response = await _factory.CrearCliente(UserId).PostAsJsonAsync("/api/foods/custom", Cuerpo(userId: 99));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await JsonAsync(response);
        Assert.Equal(9, json.GetProperty("id").GetInt32());
        Assert.Equal("Custom", json.GetProperty("source").GetString());
        Assert.Equal(80m, json.GetProperty("portionWeightGrams").GetDecimal());
        _factory.FoodManager.Verify(m => m.CreateCustomAsync(UserId, Esperado), Times.Once);
    }

    [Fact]
    public async Task Post_ArgumentException_Devuelve400ConElCampo()
    {
        _factory.FoodManager.Setup(m => m.CreateCustomAsync(UserId, It.IsAny<CustomFoodData>()))
            .ThrowsAsync(new ArgumentException("Proteína + carbohidratos + grasas suman más de 100 g.", "fatPer100g"));

        var response = await _factory.CrearCliente(UserId).PostAsJsonAsync("/api/foods/custom", Cuerpo());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(["fatPer100g"], await CamposConErrorAsync(response));
    }

    [Fact]
    public async Task Post_SinMacros_LlegaNullAlManager()
    {
        _factory.FoodManager.Setup(m => m.CreateCustomAsync(UserId, It.IsAny<CustomFoodData>())).ReturnsAsync(_torta);

        await _factory.CrearCliente(UserId).PostAsJsonAsync("/api/foods/custom", new { name = "Torta" });

        _factory.FoodManager.Verify(m => m.CreateCustomAsync(UserId, new CustomFoodData("Torta", null, null, null, null, null, null, null)), Times.Once);
    }

    [Fact]
    public async Task GetLista_LosDelUsuarioDelClaim()
    {
        _factory.FoodManager.Setup(m => m.GetCustomAsync(UserId)).ReturnsAsync([_torta]);

        var response = await _factory.CrearCliente(UserId).GetAsync("/api/foods/custom");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(9, (await JsonAsync(response))[0].GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task GetUno_ElMio200_SiNo404()
    {
        _factory.FoodManager.Setup(m => m.GetCustomByIdAsync(9, UserId)).ReturnsAsync(_torta);

        Assert.Equal(HttpStatusCode.OK, (await _factory.CrearCliente(UserId).GetAsync("/api/foods/custom/9")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _factory.CrearCliente(UserId).GetAsync("/api/foods/custom/10")).StatusCode);
    }

    [Fact]
    public async Task Put_ElMio200_DeOtro404()
    {
        _factory.FoodManager.Setup(m => m.UpdateCustomAsync(9, UserId, Esperado)).ReturnsAsync(_torta);

        var ok = await _factory.CrearCliente(UserId).PutAsJsonAsync("/api/foods/custom/9", Cuerpo());
        var otro = await _factory.CrearCliente(UserId).PutAsJsonAsync("/api/foods/custom/10", Cuerpo());

        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal("Torta", (await JsonAsync(ok)).GetProperty("name").GetString());
        Assert.Equal(HttpStatusCode.NotFound, otro.StatusCode);
    }

    [Fact]
    public async Task Put_ArgumentException_Devuelve400ConElCampo()
    {
        _factory.FoodManager.Setup(m => m.UpdateCustomAsync(9, UserId, It.IsAny<CustomFoodData>()))
            .ThrowsAsync(new ArgumentException("Falta el nombre.", "name"));

        var response = await _factory.CrearCliente(UserId).PutAsJsonAsync("/api/foods/custom/9", Cuerpo());

        Assert.Equal(["name"], await CamposConErrorAsync(response));
    }

    [Fact]
    public async Task Delete_ElMio204_DeOtro404()
    {
        _factory.FoodManager.Setup(m => m.DeleteCustomAsync(9, UserId)).ReturnsAsync(true);

        Assert.Equal(HttpStatusCode.NoContent, (await _factory.CrearCliente(UserId).DeleteAsync("/api/foods/custom/9")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _factory.CrearCliente(UserId).DeleteAsync("/api/foods/custom/10")).StatusCode);
    }

    [Fact]
    public async Task Delete_EnUnaReceta_Devuelve409ConElMensaje()
    {
        _factory.FoodManager.Setup(m => m.DeleteCustomAsync(9, UserId))
            .ThrowsAsync(new ConflictException("El alimento está en una receta: sacalo de la receta antes de borrarlo"));

        var response = await _factory.CrearCliente(UserId).DeleteAsync("/api/foods/custom/9");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("El alimento está en una receta: sacalo de la receta antes de borrarlo", (await JsonAsync(response)).GetProperty("title").GetString());
    }
}
