using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.BusinessLogic.Managers;
using AppNutricion.BusinessLogic.Services;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;
using Moq;

namespace AppNutricion.Tests.Meals;

public class MealEntryManagerTests
{
    private const int UserId = 7;
    private static readonly DateOnly Hoy = DateOnly.FromDateTime(DateTime.UtcNow);

    private readonly Mock<IMealEntryStore> _store = new();
    private readonly Mock<IFoodManager> _foods = new();
    private readonly MealEntryManager _manager;
    private readonly Food _pan = new()
    {
        Id = 3, Name = "Pan", Brand = "Bimbo",
        CaloriesPer100g = 250, ProteinPer100g = 10, CarbsPer100g = 30, FatPer100g = 5,
    };
    private readonly List<MealEntry> _agregadas = [];

    public MealEntryManagerTests()
    {
        _foods.Setup(f => f.GetByIdAsync(_pan.Id, UserId)).ReturnsAsync(_pan);
        _foods.Setup(f => f.ConvertToGrams(It.IsAny<Food>(), It.IsAny<decimal>(), false))
            .Returns((Food _, decimal q, bool _) => q);
        _store.Setup(s => s.AddAsync(It.IsAny<IEnumerable<MealEntry>>()))
            .Callback((IEnumerable<MealEntry> e) => _agregadas.AddRange(e));
        _store.Setup(s => s.UpdateAsync(It.IsAny<MealEntry>(), UserId)).ReturnsAsync(true);
        _manager = new MealEntryManager(_store.Object, _foods.Object, new UserToday(Mock.Of<IProfileStore>(), TimeProvider.System), TimeProvider.System);
    }

    private Task<MealEntry?> Crear(decimal quantity, bool isUnits = false, DateOnly? date = null, int foodId = 3, int? recipeId = null) =>
        _manager.CreateAsync(UserId, foodId, quantity, isUnits, MealType.Lunch, date ?? Hoy, recipeId);

    [Fact]
    public async Task Create_EnGramos_CongelaMacrosYDatos()
    {
        var antes = DateTime.UtcNow;

        var entry = await Crear(150);

        Assert.NotNull(entry);
        Assert.Equal(375m, entry.SavedCalories);
        Assert.Equal(15m, entry.SavedProtein);
        Assert.Equal(45m, entry.SavedCarbs);
        Assert.Equal(7.5m, entry.SavedFat);
        Assert.Equal(150m, entry.QuantityGrams);
        Assert.Equal("Pan (Bimbo)", entry.ItemName);
        Assert.Equal(UserId, entry.UserId);
        Assert.Equal(_pan.Id, entry.FoodId);
        Assert.Null(entry.RecipeId);
        Assert.Equal(MealType.Lunch, entry.MealType);
        Assert.Equal(Hoy, entry.Date);
        Assert.Equal(DateTimeKind.Utc, entry.CreatedAt.Kind);
        Assert.InRange(entry.CreatedAt, antes, DateTime.UtcNow);
        Assert.Same(entry, Assert.Single(_agregadas));
    }

    [Fact]
    public async Task Create_EnUnidades_UsaLosGramosDeConvertToGrams()
    {
        _foods.Setup(f => f.ConvertToGrams(_pan, 2, true)).Returns(60);

        var entry = await Crear(2, isUnits: true);

        Assert.NotNull(entry);
        Assert.Equal(60m, entry.QuantityGrams);
        Assert.Equal(150m, entry.SavedCalories);
        Assert.Equal(6m, entry.SavedProtein);
        Assert.Equal(18m, entry.SavedCarbs);
        Assert.Equal(3m, entry.SavedFat);
    }

    [Fact]
    public async Task Create_RedondeaLosMacrosADosDecimales()
    {
        _pan.CaloriesPer100g = 33.33m;   // × 33 g = 10.9989
        _pan.ProteinPer100g = 1.234m;    // × 33 g = 0.40722
        _pan.CarbsPer100g = 0.01m;       // × 33 g = 0.0033
        _pan.FatPer100g = 12.345m;       // × 33 g = 4.07385

        var entry = await Crear(33);

        Assert.NotNull(entry);
        Assert.Equal(11.00m, entry.SavedCalories);
        Assert.Equal(0.41m, entry.SavedProtein);
        Assert.Equal(0.00m, entry.SavedCarbs);
        Assert.Equal(4.07m, entry.SavedFat);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public async Task Create_SinMarca_ItemNameEsSoloElNombre(string? brand)
    {
        _pan.Brand = brand;

        var entry = await Crear(100);

        Assert.Equal("Pan", entry?.ItemName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(5000.01)]
    public async Task Create_GramosFueraDeRango_LanzaArgumentException(decimal quantity)
    {
        Assert.Equal("quantity", (await Assert.ThrowsAsync<ArgumentException>(() => Crear(quantity))).ParamName);
        Assert.Empty(_agregadas);
    }

    [Fact]
    public async Task Create_5000Gramos_SeAcepta()
    {
        Assert.NotNull(await Crear(5000));
    }

    [Fact]
    public async Task Create_FechaMasDeUnDiaEnElFuturo_LanzaArgumentException()
    {
        Assert.Equal("date", (await Assert.ThrowsAsync<ArgumentException>(() => Crear(100, date: Hoy.AddDays(2)))).ParamName);
        Assert.Empty(_agregadas);
    }

    [Fact]
    public async Task Create_FechaDeMananaYDeHaceDosAnios_SeAceptan()
    {
        Assert.NotNull(await Crear(100, date: Hoy.AddDays(1)));
        Assert.NotNull(await Crear(100, date: Hoy.AddYears(-2)));
    }

    [Fact]
    public async Task Create_FechaDeHaceMasDeDosAnios_LanzaArgumentException()
    {
        Assert.Equal("date", (await Assert.ThrowsAsync<ArgumentException>(() => Crear(100, date: Hoy.AddYears(-2).AddDays(-1)))).ParamName);
    }

    [Fact]
    public async Task Create_AlimentoNoVisible_DevuelveNullSinGuardar()
    {
        var entry = await Crear(100, foodId: 99);

        Assert.Null(entry);
        _foods.Verify(f => f.GetByIdAsync(99, UserId), Times.Once);
        _store.Verify(s => s.AddAsync(It.IsAny<IEnumerable<MealEntry>>()), Times.Never);
    }

    [Fact]
    public async Task Create_ConRecipeId_LanzaArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => Crear(100, recipeId: 1));
        Assert.Contains("L9", ex.Message);
        Assert.Equal("recipeId", ex.ParamName);
    }

    [Fact]
    public async Task Create_MealTypeInexistente_LanzaArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _manager.CreateAsync(UserId, _pan.Id, 100, false, (MealType)99, Hoy, null));
        Assert.Equal("mealType", ex.ParamName);
    }

    // Los valores guardados no coinciden con el pan actual (250/10/30/5 por 100 g): así se ve de dónde sale cada número.
    private MealEntry Guardada(int? foodId = 3) => new()
    {
        Id = 5, UserId = UserId, FoodId = foodId, ItemName = "Pan viejo", Date = Hoy, MealType = MealType.Lunch,
        QuantityGrams = 150, SavedCalories = 300, SavedProtein = 20, SavedCarbs = 45.55m, SavedFat = 7,
    };

    [Fact]
    public async Task Update_CambiaLaCantidadConElAlimentoVisible_RecongelaDesdeFoodsYActualizaElNombre()
    {
        var guardada = Guardada();
        _store.Setup(s => s.GetByIdAsync(5, UserId)).ReturnsAsync(guardada);

        var entry = await _manager.UpdateAsync(5, UserId, 300, false, null, null);

        Assert.NotNull(entry);
        Assert.Equal(300m, entry.QuantityGrams);
        Assert.Equal(750m, entry.SavedCalories);
        Assert.Equal(30m, entry.SavedProtein);
        Assert.Equal(90m, entry.SavedCarbs);
        Assert.Equal(15m, entry.SavedFat);
        Assert.Equal("Pan (Bimbo)", entry.ItemName);
        _foods.Verify(f => f.GetByIdAsync(_pan.Id, UserId), Times.Once);
        _store.Verify(s => s.UpdateAsync(guardada, UserId), Times.Once);
    }

    // Con 1 g guardado los macros redondeados pierden casi todo: escalarlos ×5000 da otro número.
    [Fact]
    public async Task Update_De1A5000GramosConElAlimentoVisible_DaElValorExacto()
    {
        var yerba = new Food { Id = 8, Name = "Yerba", CaloriesPer100g = 0.3m, ProteinPer100g = 1.234m, CarbsPer100g = 0.4m, FatPer100g = 0.111m };
        _foods.Setup(f => f.GetByIdAsync(8, UserId)).ReturnsAsync(yerba);
        _store.Setup(s => s.GetByIdAsync(5, UserId)).ReturnsAsync(new MealEntry
        {
            Id = 5, UserId = UserId, FoodId = 8, ItemName = "Yerba", Date = Hoy, MealType = MealType.Breakfast,
            QuantityGrams = 1, SavedCalories = 0, SavedProtein = 0.01m, SavedCarbs = 0, SavedFat = 0,
        });

        var entry = await _manager.UpdateAsync(5, UserId, 5000, false, null, null);

        Assert.Equal(5000m, entry?.QuantityGrams);
        Assert.Equal(15m, entry?.SavedCalories);
        Assert.Equal(61.7m, entry?.SavedProtein);
        Assert.Equal(20m, entry?.SavedCarbs);
        Assert.Equal(5.55m, entry?.SavedFat);
    }

    [Fact]
    public async Task Update_ConElAlimentoVisible_RedondeaA2Decimales()
    {
        _foods.Setup(f => f.GetByIdAsync(_pan.Id, UserId))
            .ReturnsAsync(new Food { Id = 3, Name = "Pan", CaloriesPer100g = 251.111m, ProteinPer100g = 9.999m, CarbsPer100g = 33.333m, FatPer100g = 4.444m });
        _store.Setup(s => s.GetByIdAsync(5, UserId)).ReturnsAsync(Guardada());

        var entry = await _manager.UpdateAsync(5, UserId, 33, false, null, null);

        Assert.Equal(82.87m, entry?.SavedCalories);   // 33 × 251.111 / 100 = 82.866…
        Assert.Equal(3.30m, entry?.SavedProtein);     // 3.29967
        Assert.Equal(11m, entry?.SavedCarbs);         // 10.99989
        Assert.Equal(1.47m, entry?.SavedFat);         // 1.46652
        Assert.Equal("Pan", entry?.ItemName);
    }

    [Fact]
    public async Task Update_AlimentoBorrado_RedondeaLosMacrosProporcionales()
    {
        _store.Setup(s => s.GetByIdAsync(5, UserId)).ReturnsAsync(Guardada(foodId: null));

        var entry = await _manager.UpdateAsync(5, UserId, 100, false, null, null);

        Assert.Equal(200m, entry?.SavedCalories);
        Assert.Equal(13.33m, entry?.SavedProtein);   // 20 × 100 / 150 = 13.333…
        Assert.Equal(30.37m, entry?.SavedCarbs);     // 45.55 × 100 / 150 = 30.3666…
        Assert.Equal(4.67m, entry?.SavedFat);        // 7 × 100 / 150 = 4.666…
        Assert.Equal("Pan viejo", entry?.ItemName);
    }

    [Fact]
    public async Task Update_EnUnidades_ConvierteConElAlimentoYRecongelaDesdeFoods()
    {
        _store.Setup(s => s.GetByIdAsync(5, UserId)).ReturnsAsync(Guardada());
        _foods.Setup(f => f.ConvertToGrams(_pan, 2, true)).Returns(75);

        var entry = await _manager.UpdateAsync(5, UserId, 2, true, null, null);

        Assert.Equal(75m, entry?.QuantityGrams);
        Assert.Equal(187.5m, entry?.SavedCalories);
    }

    [Fact]
    public async Task Update_AlimentoBorradoYCantidadEnGramos_Proporcional()
    {
        _store.Setup(s => s.GetByIdAsync(5, UserId)).ReturnsAsync(Guardada(foodId: null));

        var entry = await _manager.UpdateAsync(5, UserId, 75, null, null, null);

        Assert.Equal(75m, entry?.QuantityGrams);
        Assert.Equal(150m, entry?.SavedCalories);
        Assert.Equal(10m, entry?.SavedProtein);
    }

    [Fact]
    public async Task Update_AlimentoYaNoVisibleYCantidadEnGramos_Proporcional()
    {
        _store.Setup(s => s.GetByIdAsync(5, UserId)).ReturnsAsync(Guardada(foodId: 99));

        var entry = await _manager.UpdateAsync(5, UserId, 75, false, null, null);

        Assert.Equal(150m, entry?.SavedCalories);
        Assert.Equal("Pan viejo", entry?.ItemName);
        _foods.Verify(f => f.GetByIdAsync(99, UserId), Times.Once);
    }

    [Fact]
    public async Task Update_EnUnidadesConElAlimentoBorrado_LanzaArgumentException()
    {
        _store.Setup(s => s.GetByIdAsync(5, UserId)).ReturnsAsync(Guardada(foodId: null));

        Assert.Equal("quantity", (await Assert.ThrowsAsync<ArgumentException>(() => _manager.UpdateAsync(5, UserId, 2, true, null, null))).ParamName);
        _store.Verify(s => s.UpdateAsync(It.IsAny<MealEntry>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Update_EnUnidadesConAlimentoYaNoVisible_LanzaArgumentException()
    {
        _store.Setup(s => s.GetByIdAsync(5, UserId)).ReturnsAsync(Guardada(foodId: 99));

        Assert.Equal("quantity", (await Assert.ThrowsAsync<ArgumentException>(() => _manager.UpdateAsync(5, UserId, 2, true, null, null))).ParamName);
    }

    [Fact]
    public async Task Update_IsUnitsSinCantidad_LanzaArgumentException()
    {
        _store.Setup(s => s.GetByIdAsync(5, UserId)).ReturnsAsync(Guardada());

        Assert.Equal("isUnits", (await Assert.ThrowsAsync<ArgumentException>(() => _manager.UpdateAsync(5, UserId, null, true, null, null))).ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5001)]
    public async Task Update_GramosFueraDeRango_LanzaArgumentException(decimal quantity)
    {
        _store.Setup(s => s.GetByIdAsync(5, UserId)).ReturnsAsync(Guardada());

        Assert.Equal("quantity", (await Assert.ThrowsAsync<ArgumentException>(() => _manager.UpdateAsync(5, UserId, quantity, false, null, null))).ParamName);
    }

    [Fact]
    public async Task Update_FechaFutura_LanzaArgumentException()
    {
        _store.Setup(s => s.GetByIdAsync(5, UserId)).ReturnsAsync(Guardada());

        Assert.Equal("date", (await Assert.ThrowsAsync<ArgumentException>(() => _manager.UpdateAsync(5, UserId, null, null, null, Hoy.AddDays(2)))).ParamName);
    }

    [Fact]
    public async Task Update_MealTypeInexistente_LanzaArgumentException()
    {
        _store.Setup(s => s.GetByIdAsync(5, UserId)).ReturnsAsync(Guardada());

        Assert.Equal("mealType", (await Assert.ThrowsAsync<ArgumentException>(() => _manager.UpdateAsync(5, UserId, null, null, (MealType)99, null))).ParamName);
    }

    [Fact]
    public async Task Update_SoloFechaYTipo_NoTocaLosMacros()
    {
        _store.Setup(s => s.GetByIdAsync(5, UserId)).ReturnsAsync(Guardada());
        var ayer = Hoy.AddDays(-1);

        var entry = await _manager.UpdateAsync(5, UserId, null, null, MealType.Dinner, ayer);

        Assert.NotNull(entry);
        Assert.Equal(ayer, entry.Date);
        Assert.Equal(MealType.Dinner, entry.MealType);
        Assert.Equal(150m, entry.QuantityGrams);
        Assert.Equal(300m, entry.SavedCalories);
        Assert.Equal(20m, entry.SavedProtein);
        Assert.Equal("Pan viejo", entry.ItemName);
        _foods.Verify(f => f.GetByIdAsync(It.IsAny<int>(), It.IsAny<int?>()), Times.Never);
    }

    [Fact]
    public async Task Update_EntradaInexistenteODeOtroUsuario_DevuelveNull()
    {
        Assert.Null(await _manager.UpdateAsync(5, UserId, 100, false, null, null));
        _store.Verify(s => s.GetByIdAsync(5, UserId), Times.Once);
    }

    [Fact]
    public async Task Update_ElStoreNoLaEncuentraAlGuardar_DevuelveNull()
    {
        _store.Setup(s => s.GetByIdAsync(5, UserId)).ReturnsAsync(Guardada());
        _store.Setup(s => s.UpdateAsync(It.IsAny<MealEntry>(), UserId)).ReturnsAsync(false);

        Assert.Null(await _manager.UpdateAsync(5, UserId, 100, false, null, null));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Delete_LlamaAlStoreConElUserIdRecibido(bool existe)
    {
        _store.Setup(s => s.DeleteAsync(5, UserId)).ReturnsAsync(existe);

        Assert.Equal(existe, await _manager.DeleteAsync(5, UserId));
        _store.Verify(s => s.DeleteAsync(5, UserId), Times.Once);
    }

    [Fact]
    public async Task List_SinFecha_LanzaArgumentException()
    {
        Assert.Equal("date", (await Assert.ThrowsAsync<ArgumentException>(() => _manager.ListAsync(UserId, null))).ParamName);
    }

    [Fact]
    public async Task List_DevuelveLasDelDiaDelUsuario()
    {
        var delDia = new List<MealEntry> { Guardada() };
        _store.Setup(s => s.GetByDateAsync(UserId, Hoy, null)).ReturnsAsync(delDia);

        Assert.Same(delDia, await _manager.ListAsync(UserId, Hoy));
    }
}
