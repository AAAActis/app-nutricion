using AppNutricion.DataAccess.Stores;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;
using AppNutricion.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;

namespace AppNutricion.Tests.Recetas;

public sealed class RecipeStoreTests : IDisposable
{
    private const int Yo = 1;
    private const int Otro = 2;
    private static readonly DateTime Base = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

    private readonly BaseSqlite _db = new(Yo, Otro);
    private readonly int _arroz, _pollo, _miTorta, _guiso, _ensalada, _suyo;

    public RecipeStoreTests()
    {
        using var context = _db.NuevoContexto();
        var arroz = Alimento("Arroz", null);
        var pollo = Alimento("Pollo", null);
        var miTorta = Alimento("Torta", Yo);
        context.Foods.AddRange(arroz, pollo, miTorta);
        context.SaveChanges();
        (_arroz, _pollo, _miTorta) = (arroz.Id, pollo.Id, miTorta.Id);

        var guiso = Receta(Yo, "Guiso", (_pollo, 200), (_arroz, 100));
        var ensalada = Receta(Yo, "Ensalada", (_miTorta, 50));
        var suyo = Receta(Otro, "Aaa de otro", (_arroz, 100));
        context.Recipes.AddRange(guiso, ensalada, suyo);
        context.SaveChanges();
        (_guiso, _ensalada, _suyo) = (guiso.Id, ensalada.Id, suyo.Id);
    }

    public void Dispose() => _db.Dispose();

    private static Food Alimento(string nombre, int? owner) => new()
    {
        Name = nombre, NormalizedName = nombre.ToLowerInvariant(), Source = owner is null ? FoodSource.Argenfoods : FoodSource.Custom,
        CreatedByUserId = owner, ExternalId = owner is null ? nombre : null, CaloriesPer100g = 100, CreatedAt = Base,
    };

    private static Recipe Receta(int userId, string nombre, params (int FoodId, decimal Grams)[] ingredientes) => new()
    {
        UserId = userId, Name = nombre, Servings = 2, CreatedAt = Base,
        Ingredients = ingredientes.Select(i => new RecipeIngredient { FoodId = i.FoodId, QuantityGrams = i.Grams }).ToList(),
    };

    private RecipeStore NuevoStore() => new(_db.NuevoContexto());

    [Fact]
    public async Task GetByUser_SoloLasMias_PorNombre_ConIngredientesYSuAlimento()
    {
        var mias = await NuevoStore().GetByUserAsync(Yo);

        Assert.Equal([_ensalada, _guiso], mias.Select(r => r.Id));
        var guiso = mias[1];
        Assert.Equal([(_pollo, "Pollo", 200m), (_arroz, "Arroz", 100m)], guiso.Ingredients.Select(i => (i.FoodId, i.Food.Name, i.QuantityGrams)));
    }

    [Fact]
    public async Task GetById_LaMiaConIngredientes_LaDeOtroNull()
    {
        var guiso = await NuevoStore().GetByIdAsync(_guiso, Yo);

        Assert.NotNull(guiso);
        Assert.Equal(["Pollo", "Arroz"], guiso.Ingredients.Select(i => i.Food.Name));
        Assert.Null(await NuevoStore().GetByIdAsync(_suyo, Yo));
    }

    [Fact]
    public async Task Add_GuardaLaRecetaConSusIngredientes()
    {
        var receta = Receta(Yo, "Nueva", (_arroz, 80), (_miTorta, 20));

        await NuevoStore().AddAsync(receta);

        var guardada = await NuevoStore().GetByIdAsync(receta.Id, Yo);
        Assert.NotNull(guardada);
        Assert.Equal([(_arroz, 80m), (_miTorta, 20m)], guardada.Ingredients.Select(i => (i.FoodId, i.QuantityGrams)));
    }

    [Fact]
    public async Task Update_LaMia_ReemplazaNombrePorcionesEIngredientes()
    {
        var cambio = Receta(Otro, "Guiso liviano", (_arroz, 150), (_miTorta, 10));
        cambio.Id = _guiso;
        cambio.Servings = 4;

        Assert.True(await NuevoStore().UpdateAsync(cambio, Yo));

        var guiso = await NuevoStore().GetByIdAsync(_guiso, Yo);
        Assert.NotNull(guiso);
        Assert.Equal(("Guiso liviano", 4, Yo, Base), (guiso.Name, guiso.Servings, guiso.UserId, guiso.CreatedAt));
        Assert.Equal([(_arroz, 150m), (_miTorta, 10m)], guiso.Ingredients.Select(i => (i.FoodId, i.QuantityGrams)));
        await using var context = _db.NuevoContexto();
        Assert.Equal(2, await context.RecipeIngredients.CountAsync(i => i.RecipeId == _guiso));
    }

    [Fact]
    public async Task Update_LaDeOtro_FalseYNoCambia()
    {
        var cambio = Receta(Yo, "Mía", (_pollo, 1));
        cambio.Id = _suyo;

        Assert.False(await NuevoStore().UpdateAsync(cambio, Yo));

        var suyo = await NuevoStore().GetByIdAsync(_suyo, Otro);
        Assert.Equal(("Aaa de otro", _arroz), (suyo!.Name, suyo.Ingredients.Single().FoodId));
    }

    [Fact]
    public async Task Delete_LaMia_BorraIngredientesYSusComidasQuedanConRecipeIdNull()
    {
        await using (var context = _db.NuevoContexto())
        {
            context.MealEntries.Add(new MealEntry
            {
                UserId = Yo, RecipeId = _guiso, ItemName = "Guiso", Date = new DateOnly(2026, 10, 20), MealType = MealType.Dinner,
                QuantityGrams = 150, SavedCalories = 150, CreatedAt = Base,
            });
            await context.SaveChangesAsync();
        }

        Assert.True(await NuevoStore().DeleteAsync(_guiso, Yo));

        await using var after = _db.NuevoContexto();
        Assert.False(await after.Recipes.AnyAsync(r => r.Id == _guiso));
        Assert.False(await after.RecipeIngredients.AnyAsync(i => i.RecipeId == _guiso));
        var comida = await after.MealEntries.SingleAsync();
        Assert.Equal(((int?)null, "Guiso", 150m), (comida.RecipeId, comida.ItemName, comida.SavedCalories));
    }

    [Fact]
    public async Task Delete_LaDeOtro_FalseYSigue()
    {
        Assert.False(await NuevoStore().DeleteAsync(_suyo, Yo));
        Assert.NotNull(await NuevoStore().GetByIdAsync(_suyo, Otro));
    }
}
