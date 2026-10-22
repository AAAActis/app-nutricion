using AppNutricion.BusinessLogic.Exceptions;
using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.BusinessLogic.Managers;
using AppNutricion.BusinessLogic.Services;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace AppNutricion.Tests.Summary;

// Reloj: 22/10/2026 02:30 UTC = 21/10/2026 23:30 en Córdoba (UTC−3).
public class ResumenDiarioTests
{
    private const int UserId = 7;
    private static readonly DateOnly DiaCordoba = new(2026, 10, 21);

    private readonly FakeTimeProvider _reloj = new(new DateTimeOffset(2026, 10, 22, 2, 30, 0, TimeSpan.Zero));
    private readonly Mock<IMealEntryStore> _store = new();
    private readonly Mock<IGoalManager> _goals = new();
    private readonly Mock<IProfileStore> _perfiles = new();
    private readonly SummaryManager _manager;

    public ResumenDiarioTests()
    {
        _perfiles.Setup(p => p.GetByUserIdAsync(UserId))
            .ReturnsAsync(new Profile { UserId = UserId, TimeZoneId = "America/Argentina/Cordoba" });
        _store.Setup(s => s.SumByMealTypeAsync(UserId, It.IsAny<DateOnly>())).ReturnsAsync([]);
        _manager = new SummaryManager(_store.Object, _goals.Object, new UserToday(_perfiles.Object, _reloj));
    }

    private void ConComidas(Dictionary<MealType, MealTotals> totales) =>
        _store.Setup(s => s.SumByMealTypeAsync(UserId, DiaCordoba)).ReturnsAsync(totales);

    private void ConObjetivo(decimal kcal, decimal proteina, decimal carbos, decimal grasas) =>
        _goals.Setup(g => g.GetTargetsAsync(UserId)).ReturnsAsync(new GoalTargets(new Goal
        {
            UserId = UserId, IsActive = true, TargetCalories = kcal, TargetProteinG = proteina, TargetCarbsG = carbos, TargetFatG = grasas,
        }, null!));

    [Fact]
    public async Task DiaSinComidas_TodoEnCeroYSinTipos()
    {
        var resumen = await _manager.GetDailyAsync(UserId, DiaCordoba);

        Assert.Equal(DiaCordoba, resumen.Date);
        Assert.Equal(new MacroTotals(0, 0, 0, 0), resumen.Consumed);
        Assert.Empty(resumen.ByMealType);
    }

    [Fact]
    public async Task SinFecha_EsHoyDelUsuario()
    {
        var resumen = await _manager.GetDailyAsync(UserId, null);

        Assert.Equal(DiaCordoba, resumen.Date);   // en UTC ya es 22
        _store.Verify(s => s.SumByMealTypeAsync(UserId, DiaCordoba));
    }

    [Theory]
    [InlineData(2026, 10, 22)]   // mañana en Córdoba
    [InlineData(2024, 10, 20)]   // más de 2 años atrás
    public async Task FechaFueraDeRango_ArgumentExceptionConDate(int anio, int mes, int dia)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _manager.GetDailyAsync(UserId, new DateOnly(anio, mes, dia)));

        Assert.Equal("date", ex.ParamName);
        _store.Verify(s => s.SumByMealTypeAsync(It.IsAny<int>(), It.IsAny<DateOnly>()), Times.Never);
    }

    [Fact]
    public async Task ConComidas_SumaElDiaYDetallaPorTipoEnElOrdenDelEnumRedondeandoAlFinal()
    {
        ConComidas(new()
        {
            [MealType.Dinner] = new(100.4m, 10.04m, 1, 2, 1),
            [MealType.Breakfast] = new(100.4m, 10.04m, 3, 4, 2),
        });

        var resumen = await _manager.GetDailyAsync(UserId, DiaCordoba);

        Assert.Equal(new MacroTotals(201, 20.1m, 4, 6), resumen.Consumed);   // 200.8 → 201: no 100 + 100
        Assert.Equal(
            [new(MealType.Breakfast, new(100, 10, 3, 4), 2), new(MealType.Dinner, new(100, 10, 1, 2), 1)],
            resumen.ByMealType);
    }

    [Fact]
    public async Task ConObjetivo_RemainingYProgreso()
    {
        ConComidas(new() { [MealType.Lunch] = new(500, 30, 60.25m, 10, 2) });
        ConObjetivo(2000, 120, 241, 60);

        var resumen = await _manager.GetDailyAsync(UserId, DiaCordoba);

        Assert.Equal(new MacroTotals(2000, 120, 241, 60), resumen.Targets);
        Assert.Equal(new RemainingMacros(new(1500, false), new(90, false), new(180.8m, false), new(50, false)), resumen.Remaining);
        Assert.Equal(new MacroTotals(25, 25, 25, 16.7m), resumen.ProgressPct);
    }

    [Fact]
    public async Task TePasaste_RemainingNegativoYExceeded()
    {
        ConComidas(new() { [MealType.Dinner] = new(2300.4m, 120, 100, 80, 3) });
        ConObjetivo(2000, 120, 241, 60);

        var resumen = await _manager.GetDailyAsync(UserId, DiaCordoba);

        Assert.Equal(new RemainingMacros(new(-300, true), new(0, false), new(141, false), new(-20, true)), resumen.Remaining);
        Assert.Equal(new MacroTotals(115, 100, 41.5m, 133.3m), resumen.ProgressPct);
    }

    // El motor de Javi no genera targets en 0, pero un Goal así no puede tirar 500 por dividir por cero.
    [Fact]
    public async Task TargetEnCero_ProgresoEnCeroSinDividirPorCero()
    {
        ConComidas(new() { [MealType.Lunch] = new(500, 30, 60, 10, 2) });
        ConObjetivo(2000, 120, 241, 0);

        var resumen = await _manager.GetDailyAsync(UserId, DiaCordoba);

        Assert.Equal(0, resumen.ProgressPct!.FatG);
        Assert.Equal(new MacroLeft(-10, true), resumen.Remaining!.FatG);
    }

    [Fact]
    public async Task SinObjetivo_TargetsRemainingYProgresoNull()
    {
        ConComidas(new() { [MealType.Lunch] = new(500, 30, 60, 10, 2) });

        var resumen = await _manager.GetDailyAsync(UserId, DiaCordoba);

        Assert.Equal(500, resumen.Consumed.Calories);
        Assert.Null(resumen.Targets);
        Assert.Null(resumen.Remaining);
        Assert.Null(resumen.ProgressPct);
    }

    [Fact]
    public async Task ObjetivoSinPerfilOSinPeso_ComoSinObjetivo()
    {
        _goals.Setup(g => g.GetTargetsAsync(UserId)).ThrowsAsync(new ConflictException("Primero registrá tu peso."));

        var resumen = await _manager.GetDailyAsync(UserId, DiaCordoba);

        Assert.Null(resumen.Targets);
        Assert.Null(resumen.Remaining);
        Assert.Null(resumen.ProgressPct);
    }
}
