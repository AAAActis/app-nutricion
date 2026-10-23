using System.Net;
using System.Net.Http.Json;
using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;
using AppNutricion.Tests.Infraestructura;
using Moq;
using static AppNutricion.Tests.Infraestructura.UsuarioDePrueba;

namespace AppNutricion.Tests.Recetas;

public class RecipesControllerTests : IClassFixture<ApiFactoryAutenticada>
{
    private const int UserId = 7;

    private readonly ApiFactoryAutenticada _factory;
    private static readonly RecipeData Esperada = new("Guiso", 3, [new RecipeIngredientData(1, 200), new RecipeIngredientData(2, 100.5m)]);
    // Sin redondear, como sale del manager: el Output redondea a 1 decimal.
    private static readonly RecipeDetail Detalle = new(
        new Recipe { Id = 5, UserId = UserId, Name = "Guiso", Servings = 3, CreatedAt = new DateTime(2026, 10, 22, 15, 0, 0, DateTimeKind.Utc) },
        [new IngredientNutrition(1, "Arroz", 200, new MacroTotals(260, 5.4m, 56, 0.6m)), new IngredientNutrition(2, "Torta", 100.5m, new MacroTotals(301.5m, 5.025m, 40.2m, 12.06m))],
        new MacroTotals(561.5m, 10.425m, 96.2m, 12.66m), new MacroTotals(561.5m / 3, 10.425m / 3, 96.2m / 3, 12.66m / 3), 300.5m);

    public RecipesControllerTests(ApiFactoryAutenticada factory)
    {
        _factory = factory;
        _factory.RecipeManager.Reset();
        _factory.MealManager.Reset();
    }

    private static object Cuerpo(object? userId = null) => new
    {
        name = "Guiso", servings = 3, userId, ingredients = new[] { new { foodId = 1, quantityGrams = 200m }, new { foodId = 2, quantityGrams = 100.5m } },
    };

    private static object CargaDeReceta(object? userId = null) => new { recipeId = 5, servings = 1.5m, mealType = "Dinner", date = "2026-10-22", userId };

    public static TheoryData<string, string> Endpoints => new()
    {
        { "POST", "/api/recipes" },
        { "GET", "/api/recipes" },
        { "GET", "/api/recipes/5" },
        { "PUT", "/api/recipes/5" },
        { "DELETE", "/api/recipes/5" },
        { "POST", "/api/meals/recipe" },
    };

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task SinHeader_Devuelve401(string metodo, string ruta)
    {
        var cuerpo = ruta == "/api/meals/recipe" ? CargaDeReceta() : Cuerpo();
        var request = new HttpRequestMessage(new HttpMethod(metodo), ruta) { Content = metodo is "POST" or "PUT" ? JsonContent.Create(cuerpo) : null };

        var response = await _factory.CrearCliente(null).SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(TestAuthHandler.Esquema, response.Headers.WwwAuthenticate.ToString());
        Assert.Empty(_factory.RecipeManager.Invocations);
        Assert.Empty(_factory.MealManager.Invocations);
    }

    [Fact]
    public async Task Post_UsaElUserIdDelClaim_Devuelve201ConTotalesPorPorcionEIngredientesRedondeadosA1Decimal()
    {
        _factory.RecipeManager.Setup(m => m.CreateAsync(UserId, It.Is<RecipeData>(d => Igual(d, Esperada)))).ReturnsAsync(Detalle);

        var response = await _factory.CrearCliente(UserId).PostAsJsonAsync("/api/recipes", Cuerpo(userId: 99));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await JsonAsync(response);
        Assert.Equal((5, "Guiso", 3), (json.GetProperty("id").GetInt32(), json.GetProperty("name").GetString(), json.GetProperty("servings").GetInt32()));
        Assert.Equal(300.5m, json.GetProperty("totalGrams").GetDecimal());
        var totales = json.GetProperty("totals");
        Assert.Equal((561.5m, 10.4m, 96.2m, 12.7m), (totales.GetProperty("calories").GetDecimal(), totales.GetProperty("proteinG").GetDecimal(),
            totales.GetProperty("carbsG").GetDecimal(), totales.GetProperty("fatG").GetDecimal()));
        var porcion = json.GetProperty("perServing");
        Assert.Equal((187.2m, 3.5m, 32.1m, 4.2m), (porcion.GetProperty("calories").GetDecimal(), porcion.GetProperty("proteinG").GetDecimal(),
            porcion.GetProperty("carbsG").GetDecimal(), porcion.GetProperty("fatG").GetDecimal()));
        var torta = json.GetProperty("ingredients")[1];
        Assert.Equal((2, "Torta", 100.5m, 301.5m, 5m, 40.2m, 12.1m), (torta.GetProperty("foodId").GetInt32(), torta.GetProperty("foodName").GetString(),
            torta.GetProperty("quantityGrams").GetDecimal(), torta.GetProperty("calories").GetDecimal(), torta.GetProperty("proteinG").GetDecimal(),
            torta.GetProperty("carbsG").GetDecimal(), torta.GetProperty("fatG").GetDecimal()));
        _factory.RecipeManager.Verify(m => m.CreateAsync(99, It.IsAny<RecipeData>()), Times.Never);
    }

    private static bool Igual(RecipeData a, RecipeData b) =>
        a.Name == b.Name && a.Servings == b.Servings && a.Ingredients!.SequenceEqual(b.Ingredients!);

    [Fact]
    public async Task Post_ArgumentException_Devuelve400ConElCampoDelIngrediente()
    {
        _factory.RecipeManager.Setup(m => m.CreateAsync(UserId, It.IsAny<RecipeData>()))
            .ThrowsAsync(new ArgumentException("No existe ese alimento.", "ingredients[1].foodId"));

        var response = await _factory.CrearCliente(UserId).PostAsJsonAsync("/api/recipes", Cuerpo());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(["ingredients[1].foodId"], await CamposConErrorAsync(response));
    }

    [Fact]
    public async Task GetLista_LasDelUsuarioConTotales()
    {
        _factory.RecipeManager.Setup(m => m.ListAsync(UserId)).ReturnsAsync([Detalle]);

        var response = await _factory.CrearCliente(UserId).GetAsync("/api/recipes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var receta = (await JsonAsync(response))[0];
        Assert.Equal(5, receta.GetProperty("id").GetInt32());
        Assert.Equal(187.2m, receta.GetProperty("perServing").GetProperty("calories").GetDecimal());
    }

    [Fact]
    public async Task GetUna_LaMia200_DeOtra404()
    {
        _factory.RecipeManager.Setup(m => m.GetAsync(5, UserId)).ReturnsAsync(Detalle);

        var mia = await _factory.CrearCliente(UserId).GetAsync("/api/recipes/5");

        Assert.Equal(HttpStatusCode.OK, mia.StatusCode);
        Assert.Equal(2, (await JsonAsync(mia)).GetProperty("ingredients").GetArrayLength());
        Assert.Equal(HttpStatusCode.NotFound, (await _factory.CrearCliente(UserId).GetAsync("/api/recipes/6")).StatusCode);
    }

    [Fact]
    public async Task Put_LaMia200_DeOtra404_Invalida400()
    {
        _factory.RecipeManager.Setup(m => m.UpdateAsync(5, UserId, It.Is<RecipeData>(d => Igual(d, Esperada)))).ReturnsAsync(Detalle);
        _factory.RecipeManager.Setup(m => m.UpdateAsync(7, UserId, It.IsAny<RecipeData>())).ThrowsAsync(new ArgumentException("x", "servings"));

        Assert.Equal(HttpStatusCode.OK, (await _factory.CrearCliente(UserId).PutAsJsonAsync("/api/recipes/5", Cuerpo())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _factory.CrearCliente(UserId).PutAsJsonAsync("/api/recipes/6", Cuerpo())).StatusCode);
        Assert.Equal(["servings"], await CamposConErrorAsync(await _factory.CrearCliente(UserId).PutAsJsonAsync("/api/recipes/7", Cuerpo())));
    }

    [Fact]
    public async Task Delete_LaMia204_DeOtra404()
    {
        _factory.RecipeManager.Setup(m => m.DeleteAsync(5, UserId)).ReturnsAsync(true);

        Assert.Equal(HttpStatusCode.NoContent, (await _factory.CrearCliente(UserId).DeleteAsync("/api/recipes/5")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _factory.CrearCliente(UserId).DeleteAsync("/api/recipes/6")).StatusCode);
    }

    [Fact]
    public async Task PostComidaDeReceta_UsaElUserIdDelClaim_Devuelve201ConLaComida()
    {
        var entry = new MealEntry
        {
            Id = 30, UserId = UserId, RecipeId = 5, ItemName = "Guiso", Date = new DateOnly(2026, 10, 22), MealType = MealType.Dinner,
            QuantityGrams = 150.25m, SavedCalories = 280.75m, SavedProtein = 5.21m, SavedCarbs = 48.1m, SavedFat = 6.33m,
        };
        _factory.MealManager.Setup(m => m.CreateFromRecipeAsync(UserId, 5, 1.5m, MealType.Dinner, new DateOnly(2026, 10, 22))).ReturnsAsync(entry);

        var response = await _factory.CrearCliente(UserId).PostAsJsonAsync("/api/meals/recipe", CargaDeReceta(userId: 99));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await JsonAsync(response);
        Assert.Equal((30, 5, "Guiso", 280.75m), (json.GetProperty("id").GetInt32(), json.GetProperty("recipeId").GetInt32(),
            json.GetProperty("itemName").GetString(), json.GetProperty("savedCalories").GetDecimal()));
        Assert.Equal(System.Text.Json.JsonValueKind.Null, json.GetProperty("foodId").ValueKind);
    }

    [Fact]
    public async Task PostComidaDeReceta_RecetaDeOtra404_Invalida400ConElCampo()
    {
        _factory.MealManager.Setup(m => m.CreateFromRecipeAsync(UserId, 6, It.IsAny<decimal>(), It.IsAny<MealType>(), It.IsAny<DateOnly>()))
            .ThrowsAsync(new ArgumentException("Las porciones van de más de 0 a 20.", "servings"));

        var deOtra = await _factory.CrearCliente(UserId).PostAsJsonAsync("/api/meals/recipe", CargaDeReceta());
        var invalida = await _factory.CrearCliente(UserId).PostAsJsonAsync("/api/meals/recipe",
            new { recipeId = 6, servings = 0, mealType = "Dinner", date = "2026-10-22" });

        Assert.Equal(HttpStatusCode.NotFound, deOtra.StatusCode);
        Assert.Equal(["servings"], await CamposConErrorAsync(invalida));
    }
}
