using AppNutricion.DataAccess.Stores;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;
using AppNutricion.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;

namespace AppNutricion.Tests.Foods;

// L8 y L11: un Custom es solo de su dueño; los compartidos no se tocan por estos métodos.
public sealed class AlimentosPersonalizadosStoreTests : IDisposable
{
    private const int Yo = 1;
    private const int Otro = 2;
    private static readonly DateTime Base = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

    private readonly BaseSqlite _db = new(Yo, Otro);
    private readonly int _arroz, _miTorta, _miAlfajor, _suTorta;

    public AlimentosPersonalizadosStoreTests()
    {
        using var context = _db.NuevoContexto();
        var foods = new[]
        {
            Alimento("Arroz", FoodSource.Argenfoods, null), Alimento("Torta", FoodSource.Custom, Yo),
            Alimento("Alfajor", FoodSource.Custom, Yo), Alimento("Torta de otro", FoodSource.Custom, Otro),
        };
        context.Foods.AddRange(foods);
        context.SaveChanges();
        (_arroz, _miTorta, _miAlfajor, _suTorta) = (foods[0].Id, foods[1].Id, foods[2].Id, foods[3].Id);
    }

    public void Dispose() => _db.Dispose();

    private static Food Alimento(string nombre, FoodSource source, int? owner) => new()
    {
        Name = nombre, NormalizedName = nombre.ToLowerInvariant(), Source = source, CreatedByUserId = owner,
        ExternalId = owner is null ? nombre : null, CaloriesPer100g = 100, CreatedAt = Base,
    };

    private FoodStore NuevoStore() => new(_db.NuevoContexto());

    private async Task<Food> Leer(int id)
    {
        await using var context = _db.NuevoContexto();
        return await context.Foods.SingleAsync(f => f.Id == id);
    }

    [Fact]
    public async Task GetCustom_SoloLosCustomDelUsuario_PorNombre()
    {
        var mios = await NuevoStore().GetCustomAsync(Yo);

        Assert.Equal([_miAlfajor, _miTorta], mios.Select(f => f.Id));
    }

    // Defensa: la base solo exige dueño a los Custom, no prohíbe dueño en los demás. Uno así no es "personalizado".
    [Fact]
    public async Task GetCustom_UnNoCustomConDueño_NoEsPersonalizadoNiSeBorra()
    {
        int raro;
        await using (var context = _db.NuevoContexto())
        {
            var food = Alimento("Aaa raro", FoodSource.Argenfoods, Yo);
            food.ExternalId = "raro";
            context.Foods.Add(food);
            await context.SaveChangesAsync();
            raro = food.Id;
        }

        Assert.DoesNotContain(raro, (await NuevoStore().GetCustomAsync(Yo)).Select(f => f.Id));
        Assert.Null(await NuevoStore().GetCustomByIdAsync(raro, Yo));
        Assert.False(await NuevoStore().DeleteCustomAsync(raro, Yo));
    }

    [Fact]
    public async Task GetCustomById_ElMio_SiElDeOtroOUnCompartido_No()
    {
        Assert.Equal(_miTorta, (await NuevoStore().GetCustomByIdAsync(_miTorta, Yo))?.Id);
        Assert.Null(await NuevoStore().GetCustomByIdAsync(_suTorta, Yo));
        Assert.Null(await NuevoStore().GetCustomByIdAsync(_arroz, Yo));
    }

    [Fact]
    public async Task Add_LoGuardaConSuId()
    {
        var food = Alimento("Budín", FoodSource.Custom, Yo);

        await NuevoStore().AddAsync(food);

        Assert.NotEqual(0, food.Id);
        Assert.Equal("Budín", (await Leer(food.Id)).Name);
    }

    [Fact]
    public async Task UpdateCustom_ElMio_CambiaLosValoresYNoElDueñoNiLaFuente()
    {
        var cambio = Alimento("Torta casera", FoodSource.Argenfoods, Otro);
        cambio.Id = _miTorta;
        cambio.NormalizedName = "torta casera";
        cambio.Brand = "Mía";
        (cambio.CaloriesPer100g, cambio.ProteinPer100g, cambio.CarbsPer100g, cambio.FatPer100g) = (350, 6, 50, 14);
        (cambio.PortionName, cambio.PortionWeightGrams) = ("porción", 80);

        Assert.True(await NuevoStore().UpdateCustomAsync(cambio, Yo));

        var torta = await Leer(_miTorta);
        Assert.Equal(("Torta casera", "torta casera", "Mía"), (torta.Name, torta.NormalizedName, torta.Brand));
        Assert.Equal((350m, 6m, 50m, 14m), (torta.CaloriesPer100g, torta.ProteinPer100g, torta.CarbsPer100g, torta.FatPer100g));
        Assert.Equal(("porción", 80m), (torta.PortionName, torta.PortionWeightGrams));
        Assert.Equal((FoodSource.Custom, (int?)Yo), (torta.Source, torta.CreatedByUserId));
    }

    [Fact]
    public async Task UpdateCustom_DeOtroOCompartido_FalseYNoCambia()
    {
        foreach (var id in new[] { _suTorta, _arroz })
        {
            var cambio = Alimento("Cambiado", FoodSource.Custom, Yo);
            cambio.Id = id;

            Assert.False(await NuevoStore().UpdateCustomAsync(cambio, Yo));
            Assert.NotEqual("Cambiado", (await Leer(id)).Name);
        }
    }

    [Fact]
    public async Task DeleteCustom_ElMio_LoBorraYSusComidasQuedanConFoodIdNull()
    {
        await using (var context = _db.NuevoContexto())
        {
            context.MealEntries.Add(new MealEntry
            {
                UserId = Yo, FoodId = _miTorta, ItemName = "Torta", Date = new DateOnly(2026, 10, 20), MealType = MealType.Snack,
                QuantityGrams = 100, SavedCalories = 100, CreatedAt = Base,
            });
            await context.SaveChangesAsync();
        }

        Assert.True(await NuevoStore().DeleteCustomAsync(_miTorta, Yo));

        await using var after = _db.NuevoContexto();
        Assert.False(await after.Foods.AnyAsync(f => f.Id == _miTorta));
        var comida = await after.MealEntries.SingleAsync();
        Assert.Equal(((int?)null, "Torta", 100m), (comida.FoodId, comida.ItemName, comida.SavedCalories));
    }

    [Fact]
    public async Task DeleteCustom_DeOtroOCompartido_FalseYSigue()
    {
        Assert.False(await NuevoStore().DeleteCustomAsync(_suTorta, Yo));
        Assert.False(await NuevoStore().DeleteCustomAsync(_arroz, Yo));

        await using var context = _db.NuevoContexto();
        Assert.Equal(4, await context.Foods.CountAsync());
    }

    [Fact]
    public async Task IsInAnyRecipe_SoloSiAlgunaRecetaLoUsa()
    {
        await using (var context = _db.NuevoContexto())
        {
            context.Recipes.Add(new Recipe
            {
                UserId = Yo, Name = "Merienda", Servings = 1, CreatedAt = Base,
                Ingredients = [new RecipeIngredient { FoodId = _miTorta, QuantityGrams = 100 }],
            });
            await context.SaveChangesAsync();
        }

        Assert.True(await NuevoStore().IsInAnyRecipeAsync(_miTorta));
        Assert.False(await NuevoStore().IsInAnyRecipeAsync(_miAlfajor));
    }

    // L11: lo que ya existía (detalle, favoritos y recientes usan el mismo filtro) no deja ver el Custom de otro.
    [Fact]
    public async Task GetById_ElCustomDeOtro_NoLoVeNiOtroUsuarioNiAnonimo()
    {
        Assert.Null(await NuevoStore().GetByIdAsync(_suTorta, Yo));
        Assert.Null(await NuevoStore().GetByIdAsync(_suTorta, null));
        Assert.Equal(_suTorta, (await NuevoStore().GetByIdAsync(_suTorta, Otro))?.Id);
    }
}
