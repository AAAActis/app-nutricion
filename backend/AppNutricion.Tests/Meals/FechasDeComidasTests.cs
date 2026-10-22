using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.BusinessLogic.Managers;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace AppNutricion.Tests.Meals;

// "Hoy" es el día local del perfil (como el peso); sin perfil, hoy UTC + 1 de tolerancia.
// Reloj: 22/10/2026 02:30 UTC = 21/10/2026 23:30 en Córdoba (UTC−3).
public class FechasDeComidasTests
{
    private const int UserId = 7;
    private static readonly DateOnly DiaCordoba = new(2026, 10, 21);
    private static readonly DateOnly DiaUtc = new(2026, 10, 22);

    private readonly FakeTimeProvider _reloj = new(new DateTimeOffset(2026, 10, 22, 2, 30, 0, TimeSpan.Zero));
    private readonly Mock<IMealEntryStore> _store = new();
    private readonly Mock<IProfileStore> _perfiles = new();
    private readonly MealEntryManager _manager;

    public FechasDeComidasTests()
    {
        var foods = new Mock<IFoodManager>();
        foods.Setup(f => f.GetByIdAsync(3, UserId)).ReturnsAsync(new Food { Id = 3, Name = "Pan", CaloriesPer100g = 250 });
        foods.Setup(f => f.ConvertToGrams(It.IsAny<Food>(), It.IsAny<decimal>(), false)).Returns((Food _, decimal q, bool _) => q);
        _store.Setup(s => s.GetByIdAsync(5, UserId)).ReturnsAsync(new MealEntry
        {
            Id = 5, UserId = UserId, FoodId = 3, ItemName = "Pan", Date = DiaCordoba, MealType = MealType.Lunch, QuantityGrams = 100,
        });
        _store.Setup(s => s.UpdateAsync(It.IsAny<MealEntry>(), UserId)).ReturnsAsync(true);
        _store.Setup(s => s.GetByDateAsync(UserId, It.IsAny<DateOnly>(), It.IsAny<MealType?>())).ReturnsAsync([]);
        _manager = new MealEntryManager(_store.Object, foods.Object, _perfiles.Object, _reloj);
    }

    private void ConPerfilEnCordoba() =>
        _perfiles.Setup(p => p.GetByUserIdAsync(UserId))
            .ReturnsAsync(new Profile { UserId = UserId, TimeZoneId = "America/Argentina/Cordoba" });

    private Task<MealEntry?> Crear(DateOnly dia) => _manager.CreateAsync(UserId, 3, 100, false, MealType.Lunch, dia, null);

    private async Task AssertRechazaAsync(Func<Task> accion, string campo) =>
        Assert.Equal(campo, (await Assert.ThrowsAsync<ArgumentException>(accion)).ParamName);

    [Fact]
    public async Task ConPerfil_AntesDeMedianocheLocal_HoyEsElDiaLocalYMananaSeRechaza()
    {
        ConPerfilEnCordoba();

        Assert.NotNull(await Crear(DiaCordoba));
        await AssertRechazaAsync(() => Crear(DiaCordoba.AddDays(1)), "date");   // ya es 22 en UTC, pero no en Córdoba
    }

    [Fact]
    public async Task ConPerfil_PasadaLaMedianocheLocal_ElDiaNuevoSeAcepta()
    {
        ConPerfilEnCordoba();
        _reloj.Advance(TimeSpan.FromMinutes(30));   // 03:00 UTC = 00:00 del 22 en Córdoba

        Assert.NotNull(await Crear(DiaCordoba.AddDays(1)));
        await AssertRechazaAsync(() => Crear(DiaCordoba.AddDays(2)), "date");
    }

    [Fact]
    public async Task ConPerfil_HastaDosAniosAtrasDelDiaLocal()
    {
        ConPerfilEnCordoba();

        Assert.NotNull(await Crear(DiaCordoba.AddYears(-2)));
        await AssertRechazaAsync(() => Crear(DiaCordoba.AddYears(-2).AddDays(-1)), "date");
    }

    [Fact]
    public async Task SinPerfil_HoyUtcMasUnDiaDeTolerancia()
    {
        Assert.NotNull(await Crear(DiaUtc.AddDays(1)));
        await AssertRechazaAsync(() => Crear(DiaUtc.AddDays(2)), "date");
        Assert.NotNull(await Crear(DiaUtc.AddYears(-2)));
        await AssertRechazaAsync(() => Crear(DiaUtc.AddYears(-2).AddDays(-1)), "date");
    }

    [Fact]
    public async Task ConPerfil_EditarYCopiarUsanElMismoDiaLocal()
    {
        ConPerfilEnCordoba();
        var manana = DiaCordoba.AddDays(1);

        await AssertRechazaAsync(() => _manager.UpdateAsync(5, UserId, null, null, null, manana), "date");
        await AssertRechazaAsync(() => _manager.CopyAsync(UserId, DiaCordoba, manana, null), "toDate");
        Assert.NotNull(await _manager.UpdateAsync(5, UserId, null, null, null, DiaCordoba.AddDays(-1)));
    }

    [Fact]
    public async Task CreatedAt_SaleDelReloj()
    {
        var entry = await Crear(DiaCordoba);

        Assert.Equal(_reloj.GetUtcNow().UtcDateTime, entry?.CreatedAt);
        Assert.Equal(DateTimeKind.Utc, entry?.CreatedAt.Kind);
    }
}
