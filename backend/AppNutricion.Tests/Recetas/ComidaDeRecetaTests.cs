using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.BusinessLogic.Managers;
using AppNutricion.BusinessLogic.Services;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;
using AppNutricion.Tests.Infraestructura;
using Moq;

namespace AppNutricion.Tests.Recetas;

// Parte 3 de L9: una receta cargada como comida, con los macros congelados desde Foods (D6) y editada según D19.
public class ComidaDeRecetaTests
{
    private const int UserId = 7;
    private static readonly DateTimeOffset Ahora = new(2026, 10, 22, 15, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Hoy = new(2026, 10, 22);

    private readonly Mock<IMealEntryStore> _store = new();
    private readonly Mock<IRecipeManager> _recetas = new();
    private readonly Mock<IFoodManager> _foods = new();
    private readonly MealEntryManager _manager;
    private readonly List<MealEntry> _agregadas = [];

    // 2 porciones, 300 g en total: 560 kcal, 10,4 P, 96 C, 12,6 G.
    private static readonly RecipeDetail Guiso = new(
        new Recipe { Id = 4, UserId = UserId, Name = "Guiso", Servings = 2 }, [],
        new MacroTotals(560, 10.4m, 96, 12.6m), new MacroTotals(280, 5.2m, 48, 6.3m), 300);

    public ComidaDeRecetaTests()
    {
        _recetas.Setup(r => r.GetAsync(4, UserId)).ReturnsAsync(Guiso);
        _store.Setup(s => s.AddAsync(It.IsAny<IEnumerable<MealEntry>>())).Callback((IEnumerable<MealEntry> e) => _agregadas.AddRange(e));
        _store.Setup(s => s.UpdateAsync(It.IsAny<MealEntry>(), UserId)).ReturnsAsync(true);
        var reloj = new RelojFijo(Ahora);
        _manager = new MealEntryManager(_store.Object, _foods.Object, _recetas.Object, new UserToday(Mock.Of<IProfileStore>(), reloj), reloj);
    }

    private Task<MealEntry?> Cargar(decimal servings, int recipeId = 4, MealType tipo = MealType.Dinner, DateOnly? date = null) =>
        _manager.CreateFromRecipeAsync(UserId, recipeId, servings, tipo, date ?? Hoy);

    [Fact]
    public async Task CreateFromRecipe_UnaPorcionYMedia_GramosYMacrosProporcionalesALasPorcionesDeLaReceta()
    {
        var entry = await Cargar(1.5m);

        Assert.NotNull(entry);
        Assert.Same(entry, Assert.Single(_agregadas));
        Assert.Equal((UserId, (int?)4, (int?)null, "Guiso"), (entry.UserId, entry.RecipeId, entry.FoodId, entry.ItemName));
        Assert.Equal((Hoy, MealType.Dinner, Ahora.UtcDateTime), (entry.Date, entry.MealType, entry.CreatedAt));
        Assert.Equal(225m, entry.QuantityGrams);
        Assert.Equal((420m, 7.8m, 72m, 9.45m), (entry.SavedCalories, entry.SavedProtein, entry.SavedCarbs, entry.SavedFat));
    }

    [Fact]
    public async Task CreateFromRecipe_RedondeaGramosYMacrosADosDecimales()
    {
        var entry = await Cargar(0.5m);
        var tercio = await _manager.CreateFromRecipeAsync(UserId, 4, 2m / 3, MealType.Dinner, Hoy);

        Assert.Equal((75m, 140m, 2.6m, 24m, 3.15m), (entry!.QuantityGrams, entry.SavedCalories, entry.SavedProtein, entry.SavedCarbs, entry.SavedFat));
        Assert.Equal((100m, 186.67m, 3.47m, 32m, 4.2m), (tercio!.QuantityGrams, tercio.SavedCalories, tercio.SavedProtein, tercio.SavedCarbs, tercio.SavedFat));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(20.01)]
    public async Task CreateFromRecipe_PorcionesFueraDeRango_400ConElCampoSinGuardar(decimal servings)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => Cargar(servings));

        Assert.Equal("servings", ex.ParamName);
        Assert.Empty(_agregadas);
    }

    [Fact]
    public async Task CreateFromRecipe_VeintePorciones_SeAcepta()
    {
        Assert.Equal(3000m, (await Cargar(20))!.QuantityGrams);
    }

    // Una porción de más de 5 kg es un error de carga, como en las comidas de alimentos.
    [Fact]
    public async Task CreateFromRecipe_MasDe5000Gramos_400EnServings()
    {
        _recetas.Setup(r => r.GetAsync(5, UserId)).ReturnsAsync(Guiso with { Recipe = new Recipe { Id = 5, Name = "Olla", Servings = 1 }, TotalGrams = 3000 });

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => Cargar(2, recipeId: 5));

        Assert.Equal("servings", ex.ParamName);
        Assert.Empty(_agregadas);
    }

    [Fact]
    public async Task CreateFromRecipe_RecetaDeOtroOInexistente_NullSinGuardar()
    {
        Assert.Null(await Cargar(1, recipeId: 99));
        _recetas.Verify(r => r.GetAsync(99, UserId), Times.Once);
        Assert.Empty(_agregadas);
    }

    [Fact]
    public async Task CreateFromRecipe_FechaYTipo_SeValidanComoEnL4()
    {
        Assert.Equal("date", (await Assert.ThrowsAsync<ArgumentException>(() => Cargar(1, date: Hoy.AddDays(2)))).ParamName);
        Assert.Equal("mealType", (await Assert.ThrowsAsync<ArgumentException>(() => Cargar(1, tipo: (MealType)99))).ParamName);
        Assert.Empty(_agregadas);
    }

    [Fact]
    public async Task CreateConRecipeId_SigueRechazadoYApuntaAlEndpointDeRecetas()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _manager.CreateAsync(UserId, 1, 100, false, MealType.Lunch, Hoy, 4));

        Assert.Equal("recipeId", ex.ParamName);
        Assert.Contains("/api/meals/recipe", ex.Message);
    }

    private void ConEntrada(MealEntry entry) => _store.Setup(s => s.GetByIdAsync(entry.Id, UserId)).ReturnsAsync(entry);

    private static MealEntry DeReceta(int? recipeId) => new()
    {
        Id = 11, UserId = UserId, RecipeId = recipeId, ItemName = "Guiso viejo", Date = Hoy, MealType = MealType.Dinner,
        QuantityGrams = 150, SavedCalories = 100, SavedProtein = 1, SavedCarbs = 10, SavedFat = 2,
    };

    // D19: la receta existe → se re-congela desde la receta y Foods (no desde lo guardado), con el nombre actual.
    [Fact]
    public async Task Update_CantidadConLaRecetaTodaviaExistente_ReCongelaDesdeLaReceta()
    {
        ConEntrada(DeReceta(4));

        var entry = await _manager.UpdateAsync(11, UserId, 450, null, null, null);

        Assert.NotNull(entry);
        Assert.Equal(("Guiso", 450m), (entry.ItemName, entry.QuantityGrams));
        Assert.Equal((840m, 15.6m, 144m, 18.9m), (entry.SavedCalories, entry.SavedProtein, entry.SavedCarbs, entry.SavedFat));
        _store.Verify(s => s.UpdateAsync(entry, UserId), Times.Once);
        _foods.Verify(f => f.GetByIdAsync(It.IsAny<int>(), It.IsAny<int?>()), Times.Never);
    }

    // D19: la receta ya no está (RecipeId null por SetNull, o borrada) → proporcional a lo guardado.
    [Theory]
    [InlineData(null)]
    [InlineData(99)]
    public async Task Update_CantidadSinLaReceta_Proporcional(int? recipeId)
    {
        ConEntrada(DeReceta(recipeId));

        var entry = await _manager.UpdateAsync(11, UserId, 300, null, null, null);

        Assert.Equal(("Guiso viejo", 300m), (entry!.ItemName, entry.QuantityGrams));
        Assert.Equal((200m, 2m, 20m, 4m), (entry.SavedCalories, entry.SavedProtein, entry.SavedCarbs, entry.SavedFat));
    }

    [Fact]
    public async Task Update_EnUnidadesUnaComidaDeReceta_400EnIsUnits()
    {
        ConEntrada(DeReceta(4));

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _manager.UpdateAsync(11, UserId, 2, true, null, null));

        Assert.Equal("isUnits", ex.ParamName);
        _store.Verify(s => s.UpdateAsync(It.IsAny<MealEntry>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Update_SinCantidad_NoTocaLosMacrosNiConsultaLaReceta()
    {
        ConEntrada(DeReceta(4));

        var entry = await _manager.UpdateAsync(11, UserId, null, null, MealType.Lunch, null);

        Assert.Equal((MealType.Lunch, 100m, "Guiso viejo"), (entry!.MealType, entry.SavedCalories, entry.ItemName));
        _recetas.Verify(r => r.GetAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    // D20: copiar el día también copia las comidas de receta, con su RecipeId y sus valores congelados.
    [Fact]
    public async Task Copy_ComidaDeReceta_SeCopiaConRecipeIdYValoresCongelados()
    {
        var ayer = Hoy.AddDays(-1);
        var deReceta = DeReceta(4);
        deReceta.Date = ayer;
        _store.Setup(s => s.GetByDateAsync(UserId, Hoy, null)).ReturnsAsync([]);
        _store.Setup(s => s.GetByDateAsync(UserId, ayer, null)).ReturnsAsync([deReceta]);

        var copia = Assert.Single(await _manager.CopyAsync(UserId, ayer, Hoy, null));

        Assert.Equal(((int?)4, (int?)null, "Guiso viejo", Hoy), (copia.RecipeId, copia.FoodId, copia.ItemName, copia.Date));
        Assert.Equal((150m, 100m, 1m, 10m, 2m), (copia.QuantityGrams, copia.SavedCalories, copia.SavedProtein, copia.SavedCarbs, copia.SavedFat));
        _recetas.Verify(r => r.GetAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }
}
