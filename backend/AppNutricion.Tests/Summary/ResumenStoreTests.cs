using AppNutricion.DataAccess.Interfaces;
using AppNutricion.DataAccess.Stores;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;
using AppNutricion.Tests.Infraestructura;

namespace AppNutricion.Tests.Summary;

public sealed class ResumenStoreTests : IDisposable
{
    private const int Yo = 1;
    private const int Otro = 2;
    private static readonly DateOnly Dia = new(2026, 10, 20);

    private readonly BaseSqlite _db = new(Yo, Otro);

    public void Dispose() => _db.Dispose();

    private async Task Guardar(int userId, MealType tipo, decimal kcal, DateOnly? dia = null, decimal p = 10, decimal c = 20, decimal g = 5)
    {
        await using var context = _db.NuevoContexto();
        context.MealEntries.Add(new MealEntry
        {
            UserId = userId, ItemName = "x", Date = dia ?? Dia, MealType = tipo, QuantityGrams = 100,
            SavedCalories = kcal, SavedProtein = p, SavedCarbs = c, SavedFat = g,
            CreatedAt = DateTime.UtcNow,
        });
        await context.SaveChangesAsync();
    }

    private MealEntryStore NuevoStore() => new(_db.NuevoContexto());

    [Fact]
    public async Task SumByMealType_SumaPorTipoSoloDelUsuarioYDelDia()
    {
        await Guardar(Yo, MealType.Lunch, 100.25m);
        await Guardar(Yo, MealType.Lunch, 200, p: 4.5m, c: 1.25m, g: 0.5m);
        await Guardar(Yo, MealType.Breakfast, 50);
        await Guardar(Yo, MealType.Lunch, 999, Dia.AddDays(-1));
        await Guardar(Otro, MealType.Lunch, 999);

        var totales = await NuevoStore().SumByMealTypeAsync(Yo, Dia);

        Assert.Equal(2, totales.Count);
        Assert.Equal(new MealTotals(300.25m, 14.5m, 21.25m, 5.5m, 2), totales[MealType.Lunch]);
        Assert.Equal(new MealTotals(50, 10, 20, 5, 1), totales[MealType.Breakfast]);
    }

    [Fact]
    public async Task SumByMealType_DiaSinComidas_Vacio()
    {
        await Guardar(Otro, MealType.Lunch, 999);

        Assert.Empty(await NuevoStore().SumByMealTypeAsync(Yo, Dia));
    }

    [Fact]
    public async Task SumByDate_UnGrupoPorDiaConComidasDelRangoYSoloDelUsuario()
    {
        await Guardar(Yo, MealType.Lunch, 100, Dia.AddDays(-6));
        await Guardar(Yo, MealType.Dinner, 50.5m, Dia.AddDays(-6), p: 1, c: 2, g: 3);
        await Guardar(Yo, MealType.Lunch, 200, Dia);
        await Guardar(Yo, MealType.Lunch, 999, Dia.AddDays(-7));   // fuera del rango
        await Guardar(Yo, MealType.Lunch, 999, Dia.AddDays(1));
        await Guardar(Otro, MealType.Lunch, 999, Dia.AddDays(-3));

        var totales = await NuevoStore().SumByDateAsync(Yo, Dia.AddDays(-6), Dia);

        Assert.Equal(2, totales.Count);
        Assert.Equal(new MealTotals(150.5m, 11, 22, 8, 2), totales[Dia.AddDays(-6)]);
        Assert.Equal(new MealTotals(200, 10, 20, 5, 1), totales[Dia]);
    }

    [Fact]
    public async Task GetLoggedDates_DistintasDescendentesHastaUpToSoloDelUsuarioYConTope()
    {
        await Guardar(Yo, MealType.Lunch, 1, Dia.AddDays(-5));
        await Guardar(Yo, MealType.Lunch, 1, Dia);
        await Guardar(Yo, MealType.Dinner, 1, Dia);
        await Guardar(Yo, MealType.Lunch, 1, Dia.AddDays(-1));
        await Guardar(Yo, MealType.Lunch, 1, Dia.AddDays(-9));
        await Guardar(Yo, MealType.Lunch, 1, Dia.AddDays(1));      // después de upTo
        await Guardar(Otro, MealType.Lunch, 1, Dia.AddDays(-2));

        var store = NuevoStore();

        Assert.Equal([Dia, Dia.AddDays(-1), Dia.AddDays(-5), Dia.AddDays(-9)], await store.GetLoggedDatesAsync(Yo, Dia, 366));
        Assert.Equal([Dia, Dia.AddDays(-1)], await NuevoStore().GetLoggedDatesAsync(Yo, Dia, 2));
    }
}
