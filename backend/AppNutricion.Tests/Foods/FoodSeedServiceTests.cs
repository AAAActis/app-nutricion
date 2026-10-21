using System.Text.Json;
using System.Text.Json.Nodes;
using AppNutricion.BusinessLogic.Services;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;
using Moq;

namespace AppNutricion.Tests.Foods;

public class FoodSeedServiceTests
{
    private readonly Mock<IFoodStore> _store = new();
    private readonly ListLogger<FoodSeedService> _logger = new();
    private readonly FoodSeedService _service;
    private List<Food>? _saved;

    public FoodSeedServiceTests()
    {
        _store.Setup(s => s.UpsertSeedAsync(It.IsAny<IEnumerable<Food>>()))
            .Callback<IEnumerable<Food>>(foods => _saved = foods.ToList())
            .ReturnsAsync(() => (_saved!.Count, 0));
        _service = new FoodSeedService(_store.Object, _logger);
    }

    private static JsonObject ValidItem(string externalId = "AF-1") => new()
    {
        ["name"] = "Leche entera",
        ["source"] = "Argenfoods",
        ["externalId"] = externalId,
        ["caloriesPer100g"] = 61,
        ["proteinPer100g"] = 3.2,
        ["carbsPer100g"] = 4.8,
        ["fatPer100g"] = 3.3,
    };

    private static string ToJson(params JsonObject[] items) => new JsonArray(items).ToJsonString();

    [Theory]
    [InlineData("name", null)]
    [InlineData("name", "   ")]
    [InlineData("source", null)]
    [InlineData("source", "Custom")]
    [InlineData("source", "Verduleria")]
    [InlineData("externalId", null)]
    [InlineData("externalId", "")]
    [InlineData("caloriesPer100g", -1)]
    [InlineData("proteinPer100g", -0.5)]
    [InlineData("carbsPer100g", -2)]
    [InlineData("fatPer100g", -3)]
    [InlineData("caloriesPer100g", 900.01)]
    [InlineData("fatPer100g", 92.01)]  // 3.2 + 4.8 + 92.01 = 100.01 g
    public async Task SeedAsync_ItemInvalido_SeDescartaConWarning(string field, object? value)
    {
        var item = ValidItem();
        item[field] = value switch { null => null, string s => s, _ => JsonValue.Create(Convert.ToDecimal(value)) };

        var summary = await _service.SeedAsync(ToJson(item));

        Assert.Equal(1, summary.Discarded);
        Assert.Equal(0, summary.Inserted);
        Assert.Empty(_saved ?? []);
        Assert.Single(_logger.Warnings);
    }

    [Fact]
    public async Task SeedAsync_ItemSinCampo_SeDescarta()
    {
        var item = ValidItem();
        item.Remove("externalId");

        var summary = await _service.SeedAsync(ToJson(item));

        Assert.Equal(1, summary.Discarded);
        Assert.Empty(_saved ?? []);
    }

    [Fact]
    public async Task SeedAsync_EnLosLimites_900KcalY100Gramos_EsValido()
    {
        var item = ValidItem();
        item["caloriesPer100g"] = 900;
        item["fatPer100g"] = 92; // 3.2 + 4.8 + 92 = 100 g

        var summary = await _service.SeedAsync(ToJson(item));

        Assert.Equal(new(1, 0, 0), summary);
        Assert.Empty(_logger.Warnings);
    }

    [Fact]
    public async Task SeedAsync_ItemValido_MapeaTodosLosCamposYNormalizaElNombre()
    {
        var item = ValidItem();
        item["name"] = "  Dulce de LECHE Clásico ";
        item["brand"] = "La Marca";
        item["barcode"] = "7790000000001";
        item["source"] = "OpenFoodFacts";
        item["proteinPer100g"] = 6.456;
        item["portionName"] = "cucharada";
        item["portionWeightGrams"] = 20;
        var before = DateTime.UtcNow;

        await _service.SeedAsync(ToJson(item));

        Assert.NotNull(_saved);
        var food = Assert.Single(_saved);
        Assert.Equal("Dulce de LECHE Clásico", food.Name);
        Assert.Equal("dulce de leche clasico", food.NormalizedName);
        Assert.Equal("La Marca", food.Brand);
        Assert.Equal("7790000000001", food.Barcode);
        Assert.Equal(FoodSource.OpenFoodFacts, food.Source);
        Assert.Equal("AF-1", food.ExternalId);
        Assert.Null(food.CreatedByUserId);
        Assert.Equal(61m, food.CaloriesPer100g);
        Assert.Equal(6.46m, food.ProteinPer100g); // la columna es numeric(9,2): se redondea antes para que no parezca cambiado en cada arranque
        Assert.Equal(4.8m, food.CarbsPer100g);
        Assert.Equal(3.3m, food.FatPer100g);
        Assert.Equal("cucharada", food.PortionName);
        Assert.Equal(20m, food.PortionWeightGrams);
        Assert.Equal(DateTimeKind.Utc, food.CreatedAt.Kind);
        Assert.True(food.CreatedAt >= before);
    }

    [Fact]
    public async Task SeedAsync_ClaveRepetidaEnElArchivo_SeDescartaLaSegunda()
    {
        var summary = await _service.SeedAsync(ToJson(ValidItem("AF-1"), ValidItem("AF-1"), ValidItem("AF-2")));

        Assert.NotNull(_saved);
        Assert.Equal(2, _saved.Count);
        Assert.Equal(1, summary.Discarded);
        Assert.Single(_logger.Warnings);
    }

    [Fact]
    public async Task SeedAsync_Resumen_CombinaLoDelStoreConLosDescartados()
    {
        _store.Setup(s => s.UpsertSeedAsync(It.IsAny<IEnumerable<Food>>())).ReturnsAsync((1, 1));
        var invalid = ValidItem("AF-3");
        invalid["caloriesPer100g"] = -5;

        var summary = await _service.SeedAsync(ToJson(ValidItem("AF-1"), ValidItem("AF-2"), invalid));

        Assert.Equal(new(1, 1, 1), summary);
    }

    [Fact]
    public async Task SeedAsync_ArchivoDeEjemplo_SeDeserializaYDescartaElInvalido()
    {
        var json = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Foods", "Fixtures", "foods_seed.sample.json"));

        var summary = await _service.SeedAsync(json);

        Assert.Equal(new(4, 0, 1), summary);
        Assert.NotNull(_saved);
        Assert.Equal(["TEST-SEED-1", "TEST-SEED-2", "0000000000017", "TEST-SEED-4"], _saved!.Select(f => f.ExternalId));
        var dulce = _saved!.Single(f => f.Source == FoodSource.OpenFoodFacts);
        Assert.Equal("dulce de leche clasico", dulce.NormalizedName);
        Assert.Equal("0000000000017", dulce.Barcode);
        Assert.Contains(_logger.Warnings, w => w.Contains("TEST-SEED-5"));
    }
}
