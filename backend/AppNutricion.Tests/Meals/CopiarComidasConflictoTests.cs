using AppNutricion.BusinessLogic.Exceptions;
using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.BusinessLogic.Managers;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.DataAccess.Stores;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;
using AppNutricion.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AppNutricion.Tests.Meals;

// Copiar a un día que ya tiene comidas (del tipo pedido, o cualquiera sin tipo) → 409 (D20). Con el store real en SQLite.
public sealed class CopiarComidasConflictoTests : IDisposable
{
    private const int UserId = 1;
    private static readonly DateOnly Hoy = DateOnly.FromDateTime(DateTime.UtcNow);
    private static readonly DateOnly Ayer = Hoy.AddDays(-1);

    private readonly BaseSqlite _db = new(UserId);

    public void Dispose() => _db.Dispose();

    private MealEntryManager NuevoManager() =>
        new(new MealEntryStore(_db.NuevoContexto()), Mock.Of<IFoodManager>(), Mock.Of<IProfileStore>(), TimeProvider.System);

    private async Task Guardar(DateOnly dia, MealType tipo)
    {
        await using var context = _db.NuevoContexto();
        context.MealEntries.Add(new MealEntry
        {
            UserId = UserId, ItemName = tipo.ToString(), Date = dia, MealType = tipo, QuantityGrams = 100,
            SavedCalories = 100, CreatedAt = DateTime.UtcNow,
        });
        await context.SaveChangesAsync();
    }

    private async Task<int> CantidadEn(DateOnly dia)
    {
        await using var context = _db.NuevoContexto();
        return await context.MealEntries.CountAsync(m => m.Date == dia);
    }

    [Fact]
    public async Task CopiarDosVeces_LaSegundaLanzaConflictYNoDuplica()
    {
        await Guardar(Ayer, MealType.Breakfast);
        await Guardar(Ayer, MealType.Lunch);
        Assert.Equal(2, (await NuevoManager().CopyAsync(UserId, Ayer, Hoy, null)).Count);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => NuevoManager().CopyAsync(UserId, Ayer, Hoy, null));

        Assert.Equal("El día destino ya tiene comidas cargadas; borralas o copiá un tipo de comida que no tenga", ex.Message);
        Assert.Equal(2, await CantidadEn(Hoy));
    }

    [Fact]
    public async Task CopiarUnTipo_AlDiaQueYaTieneEseTipo_LanzaConflict()
    {
        await Guardar(Ayer, MealType.Lunch);
        await Guardar(Hoy, MealType.Lunch);

        await Assert.ThrowsAsync<ConflictException>(() => NuevoManager().CopyAsync(UserId, Ayer, Hoy, MealType.Lunch));
        Assert.Equal(1, await CantidadEn(Hoy));
    }

    [Fact]
    public async Task CopiarSoloBreakfast_AUnDiaConSoloLunch_Copia()
    {
        await Guardar(Ayer, MealType.Breakfast);
        await Guardar(Hoy, MealType.Lunch);

        var copias = await NuevoManager().CopyAsync(UserId, Ayer, Hoy, MealType.Breakfast);

        Assert.Equal(MealType.Breakfast, Assert.Single(copias).MealType);
        Assert.Equal(2, await CantidadEn(Hoy));
    }

    [Fact]
    public async Task CopiarTodo_AUnDiaConCualquierComida_LanzaConflict()
    {
        await Guardar(Ayer, MealType.Breakfast);
        await Guardar(Hoy, MealType.Dinner);

        await Assert.ThrowsAsync<ConflictException>(() => NuevoManager().CopyAsync(UserId, Ayer, Hoy, null));
    }
}
