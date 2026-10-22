using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;
using AppNutricion.Tests.Infraestructura;
using Moq;

namespace AppNutricion.Tests.Meals;

public class MealsControllerTests : IClassFixture<ApiFactoryAutenticada>
{
    private const int UserId = 7;
    private static readonly DateOnly Dia = new(2026, 10, 20);

    private readonly ApiFactoryAutenticada _factory;
    private readonly MealEntry _entrada = new()
    {
        Id = 5, UserId = UserId, FoodId = 3, ItemName = "Pan (Bimbo)", Date = Dia, MealType = MealType.Lunch,
        QuantityGrams = 150, SavedCalories = 375, SavedProtein = 15, SavedCarbs = 45, SavedFat = 7.5m,
        CreatedAt = new DateTime(2026, 10, 20, 15, 0, 0, DateTimeKind.Utc),
    };

    public MealsControllerTests(ApiFactoryAutenticada factory)
    {
        _factory = factory;
        _factory.MealManager.Reset();
    }

    private static object Alta(object? userId = null) => new
    {
        foodId = 3, quantity = 150, isUnits = false, mealType = "Lunch", date = "2026-10-20", userId,
    };

    public static TheoryData<string, string> EndpointsDeComidas => new()
    {
        { "POST", "/api/meals" },
        { "PUT", "/api/meals/5" },
        { "DELETE", "/api/meals/5" },
        { "GET", "/api/meals?date=2026-10-20" },
        { "POST", "/api/meals/copy" },
    };

    private static HttpRequestMessage Pedido(string metodo, string ruta) => new(new HttpMethod(metodo), ruta)
    {
        Content = ruta.EndsWith("/copy") ? JsonContent.Create(new { fromDate = "2026-10-19", toDate = "2026-10-20" })
            : metodo is "POST" or "PUT" ? JsonContent.Create(Alta()) : null,
    };

    [Theory]
    [MemberData(nameof(EndpointsDeComidas))]
    public async Task SinHeader_Devuelve401(string metodo, string ruta)
    {
        var response = await _factory.CrearCliente(null).SendAsync(Pedido(metodo, ruta));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(_factory.MealManager.Invocations);
    }

    [Theory]
    [MemberData(nameof(EndpointsDeComidas))]
    public async Task UserIdQueNoEsEntero_Devuelve401(string metodo, string ruta)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.Header, "abc");

        var response = await client.SendAsync(Pedido(metodo, ruta));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(_factory.MealManager.Invocations);
    }

    [Fact]
    public async Task Post_UsaElUserIdDelClaimAunqueElBodyTraigaOtro_Y_Devuelve201()
    {
        _factory.MealManager
            .Setup(m => m.CreateAsync(UserId, 3, 150, false, MealType.Lunch, Dia, null))
            .ReturnsAsync(_entrada);

        var response = await _factory.CrearCliente(UserId).PostAsJsonAsync("/api/meals", Alta(userId: 99));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(5, json.RootElement.GetProperty("id").GetInt32());
        Assert.Equal("Lunch", json.RootElement.GetProperty("mealType").GetString());
        Assert.Equal("2026-10-20", json.RootElement.GetProperty("date").GetString());
        Assert.Equal(375m, json.RootElement.GetProperty("savedCalories").GetDecimal());
        _factory.MealManager.Verify(m => m.CreateAsync(99, It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<bool>(),
            It.IsAny<MealType>(), It.IsAny<DateOnly>(), It.IsAny<int?>()), Times.Never);
    }

    [Fact]
    public async Task Post_ManagerDevuelveNull_Devuelve404ProblemDetails()
    {
        var response = await _factory.CrearCliente(UserId).PostAsJsonAsync("/api/meals", Alta());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        _factory.MealManager.Verify(m => m.CreateAsync(UserId, 3, 150, false, MealType.Lunch, Dia, null), Times.Once);
    }

    [Fact]
    public async Task Post_ManagerLanzaArgumentException_Devuelve400ProblemDetailsConElMensaje()
    {
        _factory.MealManager
            .Setup(m => m.CreateAsync(UserId, It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<bool>(),
                It.IsAny<MealType>(), It.IsAny<DateOnly>(), It.IsAny<int?>()))
            .ThrowsAsync(new ArgumentException("cantidad fuera de rango"));

        var response = await _factory.CrearCliente(UserId).PostAsJsonAsync("/api/meals", Alta());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("cantidad fuera de rango", json.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Post_ConRecipeId_LoPasaAlManager()
    {
        var response = await _factory.CrearCliente(UserId).PostAsJsonAsync("/api/meals",
            new { foodId = 3, quantity = 150, isUnits = false, mealType = "Lunch", date = "2026-10-20", recipeId = 4 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        _factory.MealManager.Verify(m => m.CreateAsync(UserId, 3, 150, false, MealType.Lunch, Dia, 4), Times.Once);
    }

    [Fact]
    public async Task Put_PasaLosCamposOpcionalesYElUserIdDelClaim()
    {
        _factory.MealManager.Setup(m => m.UpdateAsync(5, UserId, 300, null, MealType.Dinner, null)).ReturnsAsync(_entrada);

        var response = await _factory.CrearCliente(UserId).PutAsJsonAsync("/api/meals/5",
            new { quantity = 300, mealType = "Dinner", userId = 99 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(5, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task Put_DeOtroUsuarioOInexistente_Devuelve404ProblemDetails()
    {
        var response = await _factory.CrearCliente(UserId).PutAsJsonAsync("/api/meals/5", new { quantity = 300 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        _factory.MealManager.Verify(m => m.UpdateAsync(5, UserId, 300, null, null, null), Times.Once);
    }

    [Fact]
    public async Task Put_ManagerLanzaArgumentException_Devuelve400ProblemDetails()
    {
        _factory.MealManager.Setup(m => m.UpdateAsync(5, UserId, It.IsAny<decimal?>(), It.IsAny<bool?>(),
                It.IsAny<MealType?>(), It.IsAny<DateOnly?>()))
            .ThrowsAsync(new ArgumentException("sin porción"));

        var response = await _factory.CrearCliente(UserId).PutAsJsonAsync("/api/meals/5", new { quantity = 2, isUnits = true });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Delete_Existente_Devuelve204()
    {
        _factory.MealManager.Setup(m => m.DeleteAsync(5, UserId)).ReturnsAsync(true);

        var response = await _factory.CrearCliente(UserId).DeleteAsync("/api/meals/5");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Delete_DeOtroUsuarioOInexistente_Devuelve404ProblemDetails()
    {
        var response = await _factory.CrearCliente(UserId).DeleteAsync("/api/meals/5");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        _factory.MealManager.Verify(m => m.DeleteAsync(5, UserId), Times.Once);
    }

    [Fact]
    public async Task Get_DevuelveLasEntradasDelDiaDelUsuario()
    {
        _factory.MealManager.Setup(m => m.ListAsync(UserId, Dia)).ReturnsAsync([_entrada]);

        var response = await _factory.CrearCliente(UserId).GetAsync("/api/meals?date=2026-10-20");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Pan (Bimbo)", Assert.Single(json.EnumerateArray()).GetProperty("itemName").GetString());
    }

    [Fact]
    public async Task Get_SinFecha_Devuelve400ProblemDetails()
    {
        _factory.MealManager.Setup(m => m.ListAsync(UserId, null)).ThrowsAsync(new ArgumentException("Falta la fecha"));

        var response = await _factory.CrearCliente(UserId).GetAsync("/api/meals");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Copy_PasaFechasTipoYElUserIdDelClaim_Devuelve200ConLasCopias()
    {
        _factory.MealManager.Setup(m => m.CopyAsync(UserId, Dia.AddDays(-1), Dia, MealType.Lunch)).ReturnsAsync([_entrada]);

        var response = await _factory.CrearCliente(UserId).PostAsJsonAsync("/api/meals/copy",
            new { fromDate = "2026-10-19", toDate = "2026-10-20", mealType = "Lunch", userId = 99 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(5, Assert.Single(json.EnumerateArray()).GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task Copy_SinMealType_PasaNull()
    {
        _factory.MealManager.Setup(m => m.CopyAsync(UserId, Dia.AddDays(-1), Dia, null)).ReturnsAsync([]);

        var response = await _factory.CrearCliente(UserId).PostAsJsonAsync("/api/meals/copy",
            new { fromDate = "2026-10-19", toDate = "2026-10-20" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());
        _factory.MealManager.Verify(m => m.CopyAsync(UserId, Dia.AddDays(-1), Dia, null), Times.Once);
    }

    [Fact]
    public async Task Copy_ManagerLanzaArgumentException_Devuelve400ProblemDetails()
    {
        _factory.MealManager.Setup(m => m.CopyAsync(UserId, Dia, Dia, null)).ThrowsAsync(new ArgumentException("mismo día"));

        var response = await _factory.CrearCliente(UserId).PostAsJsonAsync("/api/meals/copy",
            new { fromDate = "2026-10-20", toDate = "2026-10-20" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
