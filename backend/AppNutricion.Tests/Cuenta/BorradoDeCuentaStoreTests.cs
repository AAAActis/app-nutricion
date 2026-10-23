using AppNutricion.DataAccess.Stores;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;
using AppNutricion.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;

namespace AppNutricion.Tests.Cuenta;

// El caso del bug: un Food Custom del usuario usado en una Recipe suya. RecipeIngredients.FoodId es NoAction,
// así que el orden importa: primero las Recipes (con sus ingredientes) y después el User (el resto en cascada).
// Ojo: SQLite NO reproduce el error de la FK (borrar solo el User no falla: chequea NO ACTION al final de la
// sentencia, después de las cascadas). Este test prueba que se borra todo lo del usuario y nada del otro; el
// orden contra Postgres queda con pasos manuales en el PR.
public sealed class BorradoDeCuentaStoreTests : IDisposable
{
    private const int Yo = 1;
    private const int Otro = 2;
    private static readonly DateTime Base = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

    private readonly BaseSqlite _db = new(Yo, Otro);
    private readonly int _arroz;

    public BorradoDeCuentaStoreTests()
    {
        using var context = _db.NuevoContexto();
        var arroz = Alimento("Arroz", FoodSource.Argenfoods, null);
        var miTorta = Alimento("Mi torta", FoodSource.Custom, Yo);
        var suTorta = Alimento("Su torta", FoodSource.Custom, Otro);
        context.Foods.AddRange(arroz, miTorta, suTorta);
        context.SaveChanges();
        _arroz = arroz.Id;

        foreach (var (userId, propio) in new[] { (Yo, miTorta), (Otro, suTorta) })
        {
            context.Recipes.Add(new Recipe
            {
                UserId = userId, Name = "Receta", Servings = 2, CreatedAt = Base,
                Ingredients = [new RecipeIngredient { FoodId = propio.Id, QuantityGrams = 100 }, new RecipeIngredient { FoodId = arroz.Id, QuantityGrams = 50 }],
            });
            context.Profiles.Add(new Profile
            {
                UserId = userId, BirthDate = new DateOnly(1990, 1, 1), BiologicalSex = BiologicalSex.Female, HeightCm = 165,
                ActivityLevel = ActivityLevel.Moderate, TimeZoneId = "UTC", UpdatedAt = Base,
            });
            context.BodyMetrics.Add(new BodyMetric { UserId = userId, Date = new DateOnly(2026, 10, 20), WeightKg = 60, CreatedAt = Base });
            context.MealEntries.Add(new MealEntry
            {
                UserId = userId, FoodId = propio.Id, ItemName = propio.Name, Date = new DateOnly(2026, 10, 20),
                MealType = MealType.Lunch, QuantityGrams = 100, CreatedAt = Base,
            });
            context.FavoriteFoods.Add(new FavoriteFood { UserId = userId, FoodId = propio.Id, CreatedAt = Base });
        }
        context.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private static Food Alimento(string nombre, FoodSource source, int? owner) => new()
    {
        Name = nombre, NormalizedName = nombre.ToLowerInvariant(), Source = source, CreatedByUserId = owner,
        ExternalId = owner is null ? nombre : null, CreatedAt = Base,
    };

    [Fact]
    public async Task DeleteWithData_FoodCustomUsadoEnSuReceta_BorraTodoLoSuyoYNadaDelOtro()
    {
        await new UserAccountStore(_db.NuevoContexto()).DeleteWithDataAsync(Yo);

        await using var context = _db.NuevoContexto();
        Assert.Equal([Otro], await context.Users.Select(u => u.Id).ToListAsync());
        Assert.Equal([Otro], await context.Recipes.Select(r => r.UserId).ToListAsync());
        Assert.Equal(2, await context.RecipeIngredients.CountAsync());
        Assert.Equal(["Arroz", "Su torta"], await context.Foods.OrderBy(f => f.Name).Select(f => f.Name).ToListAsync());
        Assert.Equal([Otro], await context.Profiles.Select(p => p.UserId).ToListAsync());
        Assert.Equal([Otro], await context.BodyMetrics.Select(b => b.UserId).ToListAsync());
        Assert.Equal([Otro], await context.MealEntries.Select(m => m.UserId).ToListAsync());
        Assert.Equal([Otro], await context.FavoriteFoods.Select(f => f.UserId).ToListAsync());
    }
}
