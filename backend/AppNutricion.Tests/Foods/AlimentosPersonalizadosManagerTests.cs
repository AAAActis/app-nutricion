using AppNutricion.BusinessLogic.Exceptions;
using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.BusinessLogic.Managers;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;
using AppNutricion.Tests.Infraestructura;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AppNutricion.Tests.Foods;

public class AlimentosPersonalizadosManagerTests
{
    private const int UserId = 7;
    private static readonly DateTimeOffset Ahora = new(2026, 10, 22, 15, 0, 0, TimeSpan.Zero);

    private readonly Mock<IFoodStore> _store = new();
    private readonly FoodManager _manager;
    private readonly Food _miTorta = new()
    {
        Id = 9, Name = "Torta", NormalizedName = "torta", Source = FoodSource.Custom, CreatedByUserId = UserId,
        CaloriesPer100g = 300, CreatedAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
    };

    public AlimentosPersonalizadosManagerTests()
    {
        _store.Setup(s => s.GetCustomByIdAsync(_miTorta.Id, UserId)).ReturnsAsync(_miTorta);
        _store.Setup(s => s.UpdateCustomAsync(It.IsAny<Food>(), UserId)).ReturnsAsync(true);
        _store.Setup(s => s.DeleteCustomAsync(_miTorta.Id, UserId)).ReturnsAsync(true);
        _manager = new FoodManager(_store.Object, Mock.Of<IOpenFoodFactsClient>(), NullLogger<FoodManager>.Instance, new RelojFijo(Ahora));
    }

    private static CustomFoodData Datos(string? name = "Torta de Ñandú", string? brand = null, decimal? kcal = 250, decimal? protein = 10,
        decimal? carbs = 30, decimal? fat = 5, string? portionName = null, decimal? portionGrams = null) =>
        new(name, brand, kcal, protein, carbs, fat, portionName, portionGrams);

    [Fact]
    public async Task CreateCustom_GuardaCustomDelUsuarioConElNombreNormalizadoComoEnLaBusqueda()
    {
        var food = await _manager.CreateCustomAsync(UserId, Datos(name: "  Torta  de Ñandú ", brand: " Casera ", portionName: " porción ", portionGrams: 80));

        _store.Verify(s => s.AddAsync(food), Times.Once);
        Assert.Equal(("Torta  de Ñandú", "torta de nandu", "Casera"), (food.Name, food.NormalizedName, food.Brand));
        Assert.Equal((FoodSource.Custom, (int?)UserId), (food.Source, food.CreatedByUserId));
        Assert.Equal((250m, 10m, 30m, 5m), (food.CaloriesPer100g, food.ProteinPer100g, food.CarbsPer100g, food.FatPer100g));
        Assert.Equal(("porción", (decimal?)80m), (food.PortionName, food.PortionWeightGrams));
        Assert.Null(food.Barcode);
        Assert.Null(food.ExternalId);
        Assert.Equal(Ahora.UtcDateTime, food.CreatedAt);
        Assert.Equal(DateTimeKind.Utc, food.CreatedAt.Kind);
    }

    [Fact]
    public async Task CreateCustom_SinMarcaNiPorcion_QuedanNull()
    {
        var food = await _manager.CreateCustomAsync(UserId, Datos(brand: "  ", portionName: " "));

        Assert.Null(food.Brand);
        Assert.Null(food.PortionName);
        Assert.Null(food.PortionWeightGrams);
    }

    // Bordes que valen: nombre de 120, kcal 0 y 900, macros que suman justo 100, porción de 5000 g.
    [Theory]
    [InlineData(120, 0, 100, 0, 0, 5000)]
    [InlineData(1, 900, 0, 100, 0, 0.01)]
    [InlineData(5, 450, 0, 0, 100, 1)]
    [InlineData(5, 400, 33.33, 33.33, 33.34, 1)]
    public async Task CreateCustom_EnLosBordes_LoAcepta(int largo, decimal kcal, decimal p, decimal c, decimal f, decimal gramos)
    {
        var food = await _manager.CreateCustomAsync(UserId, Datos(new string('a', largo), null, kcal, p, c, f, "unidad", gramos));

        Assert.Equal(kcal, food.CaloriesPer100g);
        _store.Verify(s => s.AddAsync(food), Times.Once);
    }

    public static TheoryData<CustomFoodData, string> Invalidos => new()
    {
        { Datos(name: null), "name" },
        { Datos(name: "   "), "name" },
        { Datos(name: new string('a', 121)), "name" },
        { Datos(brand: new string('b', 121)), "brand" },
        { Datos(kcal: null), "caloriesPer100g" },
        { Datos(kcal: -0.01m), "caloriesPer100g" },
        { Datos(kcal: 900.01m), "caloriesPer100g" },
        { Datos(protein: null), "proteinPer100g" },
        { Datos(protein: -1), "proteinPer100g" },
        { Datos(carbs: null), "carbsPer100g" },
        { Datos(carbs: -1), "carbsPer100g" },
        { Datos(fat: null), "fatPer100g" },
        { Datos(fat: -1), "fatPer100g" },
        { Datos(protein: 40, carbs: 40, fat: 20.01m), "fatPer100g" },
        { Datos(portionName: "unidad"), "portionWeightGrams" },
        { Datos(portionName: "unidad", portionGrams: 0), "portionWeightGrams" },
        { Datos(portionName: "unidad", portionGrams: 5000.01m), "portionWeightGrams" },
        { Datos(portionGrams: 80), "portionName" },
        { Datos(portionName: new string('p', 61), portionGrams: 80), "portionName" },
    };

    [Theory]
    [MemberData(nameof(Invalidos))]
    public async Task CreateCustom_DatoInvalido_ArgumentExceptionConElCampoSinGuardar(CustomFoodData datos, string campo)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _manager.CreateCustomAsync(UserId, datos));

        Assert.Equal(campo, ex.ParamName);
        _store.Verify(s => s.AddAsync(It.IsAny<Food>()), Times.Never);
    }

    [Fact]
    public async Task GetCustom_DevuelveLosDelUsuario()
    {
        _store.Setup(s => s.GetCustomAsync(UserId)).ReturnsAsync([_miTorta]);

        Assert.Same(_miTorta, Assert.Single(await _manager.GetCustomAsync(UserId)));
        Assert.Same(_miTorta, await _manager.GetCustomByIdAsync(_miTorta.Id, UserId));
        Assert.Null(await _manager.GetCustomByIdAsync(123, UserId));
    }

    [Fact]
    public async Task UpdateCustom_ElMio_ReemplazaLosValoresYConservaIdDueñoYAlta()
    {
        Food? guardado = null;
        _store.Setup(s => s.UpdateCustomAsync(It.IsAny<Food>(), UserId)).Callback((Food f, int _) => guardado = f).ReturnsAsync(true);

        var food = await _manager.UpdateCustomAsync(_miTorta.Id, UserId, Datos(name: "Budín Ñ", kcal: 400, portionName: "feta", portionGrams: 30));

        Assert.NotNull(food);
        Assert.Same(food, guardado);
        Assert.Equal((_miTorta.Id, "Budín Ñ", "budin n", 400m), (food.Id, food.Name, food.NormalizedName, food.CaloriesPer100g));
        Assert.Equal(("feta", (decimal?)30m), (food.PortionName, food.PortionWeightGrams));
        Assert.Equal((FoodSource.Custom, (int?)UserId, _miTorta.CreatedAt), (food.Source, food.CreatedByUserId, food.CreatedAt));
    }

    [Fact]
    public async Task UpdateCustom_DeOtroONoCustom_NullSinGuardar()
    {
        Assert.Null(await _manager.UpdateCustomAsync(123, UserId, Datos()));
        _store.Verify(s => s.UpdateCustomAsync(It.IsAny<Food>(), It.IsAny<int>()), Times.Never);
    }

    [Theory]
    [MemberData(nameof(Invalidos))]
    public async Task UpdateCustom_DatoInvalido_ArgumentExceptionConElCampoSinGuardar(CustomFoodData datos, string campo)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _manager.UpdateCustomAsync(_miTorta.Id, UserId, datos));

        Assert.Equal(campo, ex.ParamName);
        _store.Verify(s => s.UpdateCustomAsync(It.IsAny<Food>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task DeleteCustom_ElMio_LoBorra()
    {
        Assert.True(await _manager.DeleteCustomAsync(_miTorta.Id, UserId));
        _store.Verify(s => s.DeleteCustomAsync(_miTorta.Id, UserId), Times.Once);
    }

    [Fact]
    public async Task DeleteCustom_DeOtroONoCustom_FalseSinBorrar()
    {
        Assert.False(await _manager.DeleteCustomAsync(123, UserId));
        _store.Verify(s => s.DeleteCustomAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task DeleteCustom_EnUnaReceta_ConflictSinBorrar()
    {
        _store.Setup(s => s.IsInAnyRecipeAsync(_miTorta.Id)).ReturnsAsync(true);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => _manager.DeleteCustomAsync(_miTorta.Id, UserId));

        Assert.Equal("El alimento está en una receta: sacalo de la receta antes de borrarlo", ex.Message);
        _store.Verify(s => s.DeleteCustomAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }
}
