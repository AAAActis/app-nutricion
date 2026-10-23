using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.BusinessLogic.Managers;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;
using AppNutricion.Tests.Infraestructura;
using Moq;

namespace AppNutricion.Tests.Recetas;

public class RecipeManagerTests
{
    private const int UserId = 7;
    private static readonly DateTimeOffset Ahora = new(2026, 10, 22, 15, 0, 0, TimeSpan.Zero);

    private readonly Mock<IRecipeStore> _store = new();
    private readonly Mock<IFoodManager> _foods = new();
    private readonly RecipeManager _manager;
    private static readonly Food Arroz = new()
    {
        Id = 1, Name = "Arroz", Source = FoodSource.Argenfoods, CaloriesPer100g = 130, ProteinPer100g = 2.7m, CarbsPer100g = 28, FatPer100g = 0.3m,
    };
    private static readonly Food MiTorta = new()
    {
        Id = 2, Name = "Torta", Source = FoodSource.Custom, CreatedByUserId = UserId,
        CaloriesPer100g = 300, ProteinPer100g = 5, CarbsPer100g = 40, FatPer100g = 12,
    };
    private readonly List<Recipe> _agregadas = [];

    public RecipeManagerTests()
    {
        // Lo que el usuario ve: el compartido y su Custom. El 3 es el Custom de otro (no visible: null).
        _foods.Setup(f => f.GetByIdAsync(Arroz.Id, UserId)).ReturnsAsync(Arroz);
        _foods.Setup(f => f.GetByIdAsync(MiTorta.Id, UserId)).ReturnsAsync(MiTorta);
        _store.Setup(s => s.AddAsync(It.IsAny<Recipe>())).Callback((Recipe r) => _agregadas.Add(r));
        _store.Setup(s => s.UpdateAsync(It.IsAny<Recipe>(), UserId)).ReturnsAsync(true);
        _manager = new RecipeManager(_store.Object, _foods.Object, new RelojFijo(Ahora));
    }

    private static RecipeData Datos(string? name = "Arroz con torta", int servings = 2, params (int FoodId, decimal Grams)[] ingredientes) =>
        new(name, servings, (ingredientes.Length == 0 ? [(Arroz.Id, 200m), (MiTorta.Id, 100m)] : ingredientes)
            .Select(i => new RecipeIngredientData(i.FoodId, i.Grams)).ToList());

    private static Recipe Guardada(int id, int servings = 2) => new()
    {
        Id = id, UserId = UserId, Name = "Guardada", Servings = servings, CreatedAt = Ahora.UtcDateTime,
        Ingredients =
        [
            new RecipeIngredient { FoodId = Arroz.Id, Food = Arroz, QuantityGrams = 200 },
            new RecipeIngredient { FoodId = MiTorta.Id, Food = MiTorta, QuantityGrams = 100 },
        ],
    };

    // 200 g de arroz (260 kcal, 5,4 P, 56 C, 0,6 G) + 100 g de torta (300, 5, 40, 12) = 560, 10,4, 96, 12,6 en 300 g.
    private static void AssertArrozConTorta(RecipeDetail detalle, int servings)
    {
        Assert.Equal(300m, detalle.TotalGrams);
        Assert.Equal(new MacroTotals(560, 10.4m, 96, 12.6m), detalle.Totals);
        Assert.Equal(new MacroTotals(560m / servings, 10.4m / servings, 96m / servings, 12.6m / servings), detalle.PerServing);
        Assert.Equal(
            [new IngredientNutrition(Arroz.Id, "Arroz", 200, new MacroTotals(260, 5.4m, 56, 0.6m)), new IngredientNutrition(MiTorta.Id, "Torta", 100, new MacroTotals(300, 5, 40, 12))],
            detalle.Ingredients);
    }

    [Fact]
    public async Task Create_GuardaLaRecetaDelUsuarioYDevuelveTotalesYPorPorcionDesdeFoods()
    {
        var detalle = await _manager.CreateAsync(UserId, Datos(name: "  Arroz con torta ", servings: 2));

        var receta = Assert.Single(_agregadas);
        Assert.Same(receta, detalle.Recipe);
        Assert.Equal((UserId, "Arroz con torta", 2, Ahora.UtcDateTime), (receta.UserId, receta.Name, receta.Servings, receta.CreatedAt));
        Assert.Equal([(Arroz.Id, 200m), (MiTorta.Id, 100m)], receta.Ingredients.Select(i => (i.FoodId, i.QuantityGrams)));
        // Sin el Food cargado: si no, EF intentaría insertar el alimento leído sin tracking.
        Assert.All(receta.Ingredients, i => Assert.Null(i.Food));
        AssertArrozConTorta(detalle, 2);
    }

    [Fact]
    public async Task Create_PorPorcion_SinRedondear()
    {
        var detalle = await _manager.CreateAsync(UserId, Datos(servings: 3));

        Assert.Equal(560m / 3, detalle.PerServing.Calories);
    }

    // Bordes que valen: nombre de 1 y 120, 1 y 50 porciones, 1 y 30 ingredientes, 5000 g.
    [Theory]
    [InlineData(1, 1, 1, 5000)]
    [InlineData(120, 50, 30, 0.01)]
    public async Task Create_EnLosBordes_LaAcepta(int largo, int servings, int ingredientes, decimal gramos)
    {
        for (var id = 100; id < 100 + ingredientes; id++)
            _foods.Setup(f => f.GetByIdAsync(id, UserId)).ReturnsAsync(new Food { Id = id, Name = $"a{id}", CaloriesPer100g = 100 });

        var detalle = await _manager.CreateAsync(UserId,
            Datos(new string('r', largo), servings, Enumerable.Range(100, ingredientes).Select(id => (id, gramos)).ToArray()));

        Assert.Equal(ingredientes, detalle.Recipe.Ingredients.Count);
    }

    public static TheoryData<RecipeData, string> Invalidas => new()
    {
        { Datos(name: null), "name" },
        { Datos(name: "  "), "name" },
        { Datos(name: new string('r', 121)), "name" },
        { Datos(servings: 0), "servings" },
        { Datos(servings: 51), "servings" },
        { new RecipeData("Vacía", 1, null), "ingredients" },
        { new RecipeData("Vacía", 1, []), "ingredients" },
        { new RecipeData("Larga", 1, Enumerable.Range(100, 31).Select(id => new RecipeIngredientData(id, 1)).ToList()), "ingredients" },
        { Datos("R", 1, (Arroz.Id, 10), (MiTorta.Id, 0)), "ingredients[1].quantityGrams" },
        { Datos("R", 1, (Arroz.Id, 10), (MiTorta.Id, 5000.01m)), "ingredients[1].quantityGrams" },
        { Datos("R", 1, (Arroz.Id, 10), (Arroz.Id, 20)), "ingredients[1].foodId" },
        { Datos("R", 1, (Arroz.Id, 10), (3, 20)), "ingredients[1].foodId" },
    };

    [Theory]
    [MemberData(nameof(Invalidas))]
    public async Task Create_DatoInvalido_ArgumentExceptionConElCampoSinGuardar(RecipeData datos, string campo)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _manager.CreateAsync(UserId, datos));

        Assert.Equal(campo, ex.ParamName);
        _store.Verify(s => s.AddAsync(It.IsAny<Recipe>()), Times.Never);
    }

    // Lo obligatorio: el Custom de otro no puede entrar en una receta (DELETE /api/me del otro fallaría por la FK).
    [Fact]
    public async Task Create_ConElCustomDeOtro_400SinGuardarYSePreguntaConElUserIdDelDueñoDeLaReceta()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _manager.CreateAsync(UserId, Datos("R", 1, (3, 20))));

        Assert.Equal("ingredients[0].foodId", ex.ParamName);
        _foods.Verify(f => f.GetByIdAsync(3, UserId), Times.Once);
        _store.Verify(s => s.AddAsync(It.IsAny<Recipe>()), Times.Never);
    }

    [Fact]
    public async Task List_LasDelUsuarioConSusTotales()
    {
        _store.Setup(s => s.GetByUserAsync(UserId)).ReturnsAsync([Guardada(5), Guardada(6, servings: 4)]);

        var lista = await _manager.ListAsync(UserId);

        Assert.Equal([5, 6], lista.Select(d => d.Recipe.Id));
        AssertArrozConTorta(lista[0], 2);
        AssertArrozConTorta(lista[1], 4);
    }

    [Fact]
    public async Task Get_LaMiaConTotales_LaDeOtroNull()
    {
        _store.Setup(s => s.GetByIdAsync(5, UserId)).ReturnsAsync(Guardada(5));

        AssertArrozConTorta((await _manager.GetAsync(5, UserId))!, 2);
        Assert.Null(await _manager.GetAsync(6, UserId));
    }

    [Fact]
    public async Task Update_LaMia_ReemplazaTodoYDevuelveLosTotalesNuevos()
    {
        _store.Setup(s => s.GetByIdAsync(5, UserId)).ReturnsAsync(Guardada(5));
        Recipe? guardada = null;
        _store.Setup(s => s.UpdateAsync(It.IsAny<Recipe>(), UserId)).Callback((Recipe r, int _) => guardada = r).ReturnsAsync(true);

        var detalle = await _manager.UpdateAsync(5, UserId, Datos("Solo arroz", 1, (Arroz.Id, 100)));

        Assert.NotNull(detalle);
        Assert.NotNull(guardada);
        Assert.Equal((5, UserId, "Solo arroz", 1), (guardada.Id, guardada.UserId, guardada.Name, guardada.Servings));
        Assert.Equal([(Arroz.Id, 100m)], guardada.Ingredients.Select(i => (i.FoodId, i.QuantityGrams)));
        Assert.All(guardada.Ingredients, i => Assert.Null(i.Food));
        Assert.Equal(new MacroTotals(130, 2.7m, 28, 0.3m), detalle.Totals);
        Assert.Equal(detalle.Totals, detalle.PerServing);
    }

    [Fact]
    public async Task Update_LaDeOtro_NullSinGuardar()
    {
        Assert.Null(await _manager.UpdateAsync(6, UserId, Datos()));
        _store.Verify(s => s.UpdateAsync(It.IsAny<Recipe>(), It.IsAny<int>()), Times.Never);
    }

    [Theory]
    [MemberData(nameof(Invalidas))]
    public async Task Update_DatoInvalido_ArgumentExceptionConElCampoSinGuardar(RecipeData datos, string campo)
    {
        _store.Setup(s => s.GetByIdAsync(5, UserId)).ReturnsAsync(Guardada(5));

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _manager.UpdateAsync(5, UserId, datos));

        Assert.Equal(campo, ex.ParamName);
        _store.Verify(s => s.UpdateAsync(It.IsAny<Recipe>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Delete_DelegaConElUserId()
    {
        _store.Setup(s => s.DeleteAsync(5, UserId)).ReturnsAsync(true);

        Assert.True(await _manager.DeleteAsync(5, UserId));
        Assert.False(await _manager.DeleteAsync(6, UserId));
    }
}
