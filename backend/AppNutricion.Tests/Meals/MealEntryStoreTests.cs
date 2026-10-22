using AppNutricion.DataAccess.Stores;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;
using AppNutricion.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;

namespace AppNutricion.Tests.Meals;

public sealed class MealEntryStoreTests : IDisposable
{
    private const int Yo = 1;
    private const int Otro = 2;
    private static readonly DateOnly Dia = new(2026, 10, 20);
    private static readonly DateTime Base = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

    private readonly BaseSqlite _db = new(Yo, Otro);

    public void Dispose() => _db.Dispose();

    private static MealEntry Entrada(int userId, MealType tipo, int minuto, DateOnly? dia = null, string nombre = "x") => new()
    {
        UserId = userId, ItemName = nombre, Date = dia ?? Dia, MealType = tipo, QuantityGrams = 100,
        SavedCalories = 100, CreatedAt = Base.AddMinutes(minuto),
    };

    private async Task<int> Guardar(MealEntry entry)
    {
        await using var context = _db.NuevoContexto();
        context.MealEntries.Add(entry);
        await context.SaveChangesAsync();
        return entry.Id;
    }

    private MealEntryStore NuevoStore() => new(_db.NuevoContexto());

    [Fact]
    public async Task GetByDate_SoloDelUsuarioYDelDia_OrdenadasPorTipoSegunElEnumYDespuesPorHora()
    {
        await Guardar(Entrada(Yo, MealType.Dinner, 0, nombre: "cena"));
        await Guardar(Entrada(Yo, MealType.AfternoonSnack, 0, nombre: "merienda"));
        await Guardar(Entrada(Yo, MealType.Breakfast, 5, nombre: "desayuno 2"));
        await Guardar(Entrada(Yo, MealType.Lunch, 0, nombre: "almuerzo"));
        await Guardar(Entrada(Yo, MealType.Breakfast, 1, nombre: "desayuno 1"));
        await Guardar(Entrada(Yo, MealType.Lunch, 0, dia: Dia.AddDays(1), nombre: "otro día"));
        await Guardar(Entrada(Otro, MealType.Breakfast, 0, nombre: "de otro"));

        var entries = await NuevoStore().GetByDateAsync(Yo, Dia, null);

        Assert.Equal(["desayuno 1", "desayuno 2", "almuerzo", "merienda", "cena"], entries.Select(e => e.ItemName));
    }

    [Fact]
    public async Task GetByDate_ConMealType_SoloEseTipo()
    {
        await Guardar(Entrada(Yo, MealType.Breakfast, 0, nombre: "desayuno"));
        await Guardar(Entrada(Yo, MealType.Lunch, 0, nombre: "almuerzo"));

        var entries = await NuevoStore().GetByDateAsync(Yo, Dia, MealType.Lunch);

        Assert.Equal("almuerzo", Assert.Single(entries).ItemName);
    }

    [Fact]
    public async Task GetById_DeOtroUsuario_DevuelveNull()
    {
        var id = await Guardar(Entrada(Otro, MealType.Lunch, 0));

        Assert.Null(await NuevoStore().GetByIdAsync(id, Yo));
        Assert.NotNull(await NuevoStore().GetByIdAsync(id, Otro));
    }

    [Fact]
    public async Task Add_GuardaTodasYLesAsignaId()
    {
        var entries = new[] { Entrada(Yo, MealType.Lunch, 0), Entrada(Yo, MealType.Dinner, 1) };

        await NuevoStore().AddAsync(entries);

        Assert.All(entries, e => Assert.NotEqual(0, e.Id));
        await using var context = _db.NuevoContexto();
        Assert.Equal(2, await context.MealEntries.CountAsync());
    }

    [Fact]
    public async Task Update_Propia_GuardaLosCambios()
    {
        var id = await Guardar(Entrada(Yo, MealType.Lunch, 0));
        var entry = Entrada(Yo, MealType.Dinner, 0, nombre: "nombre nuevo");
        entry.Id = id;
        entry.QuantityGrams = 200;
        entry.SavedCalories = 200;

        Assert.True(await NuevoStore().UpdateAsync(entry, Yo));

        await using var context = _db.NuevoContexto();
        var guardada = await context.MealEntries.SingleAsync();
        Assert.Equal(200m, guardada.QuantityGrams);
        Assert.Equal(200m, guardada.SavedCalories);
        Assert.Equal(MealType.Dinner, guardada.MealType);
        Assert.Equal("nombre nuevo", guardada.ItemName);
    }

    [Fact]
    public async Task Update_DeOtroUsuario_DevuelveFalseYNoLaToca()
    {
        var id = await Guardar(Entrada(Otro, MealType.Lunch, 0));
        var ajena = Entrada(Otro, MealType.Dinner, 0);
        ajena.Id = id;
        ajena.QuantityGrams = 999;

        Assert.False(await NuevoStore().UpdateAsync(ajena, Yo));

        await using var context = _db.NuevoContexto();
        Assert.Equal(100m, (await context.MealEntries.SingleAsync()).QuantityGrams);
    }

    [Fact]
    public async Task Delete_DeOtroUsuario_DevuelveFalseYNoLaBorra()
    {
        var id = await Guardar(Entrada(Otro, MealType.Lunch, 0));

        Assert.False(await NuevoStore().DeleteAsync(id, Yo));

        await using var context = _db.NuevoContexto();
        Assert.Equal(1, await context.MealEntries.CountAsync());
    }

    [Fact]
    public async Task Delete_Propia_LaBorra()
    {
        var id = await Guardar(Entrada(Yo, MealType.Lunch, 0));

        Assert.True(await NuevoStore().DeleteAsync(id, Yo));

        await using var context = _db.NuevoContexto();
        Assert.Equal(0, await context.MealEntries.CountAsync());
    }
}
