using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.BusinessLogic.Managers;
using AppNutricion.BusinessLogic.Services;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace AppNutricion.Tests.Summary;

// Reloj: 22/10/2026 02:30 UTC = 21/10/2026 23:30 en Córdoba (UTC−3): hoy es el 21.
public class ResumenSemanalTests
{
    private const int UserId = 7;
    private static readonly DateOnly Hoy = new(2026, 10, 21);

    private readonly FakeTimeProvider _reloj = new(new DateTimeOffset(2026, 10, 22, 2, 30, 0, TimeSpan.Zero));
    private readonly Mock<IMealEntryStore> _store = new();
    private readonly Mock<IGoalManager> _goals = new();
    private readonly Mock<IProfileStore> _perfiles = new();
    private readonly SummaryManager _manager;

    public ResumenSemanalTests()
    {
        _perfiles.Setup(p => p.GetByUserIdAsync(UserId))
            .ReturnsAsync(new Profile { UserId = UserId, TimeZoneId = "America/Argentina/Cordoba" });
        _store.Setup(s => s.SumByDateAsync(UserId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>())).ReturnsAsync([]);
        _store.Setup(s => s.GetLoggedDatesAsync(UserId, It.IsAny<DateOnly>(), It.IsAny<int>())).ReturnsAsync([]);
        _manager = new SummaryManager(_store.Object, _goals.Object, new UserToday(_perfiles.Object, _reloj));
    }

    // Como el store: del más reciente al más viejo.
    private void ConDiasCargados(params int[] diasAtras) =>
        _store.Setup(s => s.GetLoggedDatesAsync(UserId, Hoy, 366))
            .ReturnsAsync(diasAtras.Order().Select(d => Hoy.AddDays(-d)).ToList());

    private async Task<int> Racha() => (await _manager.GetWeeklyAsync(UserId, null)).Streak;

    [Fact]
    public async Task SemanaConHuecos_LosSieteDiasEnOrdenYLosVaciosEnCero()
    {
        var fin = new DateOnly(2026, 10, 18);
        _store.Setup(s => s.SumByDateAsync(UserId, fin.AddDays(-6), fin)).ReturnsAsync(new Dictionary<DateOnly, MealTotals>
        {
            [fin] = new(1800.4m, 100.04m, 200, 60, 3),
            [fin.AddDays(-6)] = new(2000, 120, 220, 70, 4),
        });

        var semana = await _manager.GetWeeklyAsync(UserId, fin);

        Assert.Equal(Enumerable.Range(0, 7).Select(i => fin.AddDays(i - 6)), semana.Days.Select(d => d.Date));
        Assert.Equal(new DayTotals(fin.AddDays(-6), new(2000, 120, 220, 70), true), semana.Days[0]);
        Assert.Equal(new DayTotals(fin.AddDays(-3), new(0, 0, 0, 0), false), semana.Days[3]);
        Assert.Equal(new DayTotals(fin, new(1800, 100, 200, 60), true), semana.Days[6]);
        Assert.Equal(2, semana.DaysLogged);
    }

    [Fact]
    public async Task Promedio_IgnoraLosDiasVacios()
    {
        _store.Setup(s => s.SumByDateAsync(UserId, Hoy.AddDays(-6), Hoy)).ReturnsAsync(new Dictionary<DateOnly, MealTotals>
        {
            [Hoy] = new(1500, 100, 150, 50.25m, 2),
            [Hoy.AddDays(-2)] = new(2000.5m, 120, 200, 70, 3),
            [Hoy.AddDays(-4)] = new(2500, 140, 250, 90, 1),
        });

        var semana = await _manager.GetWeeklyAsync(UserId, null);

        Assert.Equal(new MacroTotals(2000, 120, 200, 70.1m), semana.Averages);   // 6000.5/3 y 210.25/3: no /7
    }

    [Fact]
    public async Task SinComidas_PromedioNullYCeroDias()
    {
        var semana = await _manager.GetWeeklyAsync(UserId, null);

        Assert.Null(semana.Averages);
        Assert.Equal(0, semana.DaysLogged);
        Assert.Equal(7, semana.Days.Count);
        Assert.All(semana.Days, d => Assert.False(d.Logged));
    }

    [Fact]
    public async Task SinEndDate_TerminaHoyDelUsuario()
    {
        var semana = await _manager.GetWeeklyAsync(UserId, null);

        Assert.Equal(Hoy, semana.Days[^1].Date);
        _store.Verify(s => s.SumByDateAsync(UserId, Hoy.AddDays(-6), Hoy));
    }

    [Fact]
    public async Task EndDateFueraDeRango_ArgumentExceptionConEndDate()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _manager.GetWeeklyAsync(UserId, Hoy.AddDays(1)));

        Assert.Equal("endDate", ex.ParamName);
    }

    [Fact]
    public async Task TargetCalories_DelObjetivoActivoONull()
    {
        Assert.Null((await _manager.GetWeeklyAsync(UserId, null)).TargetCalories);

        _goals.Setup(g => g.GetTargetsAsync(UserId)).ReturnsAsync(new GoalTargets(new Goal { TargetCalories = 2150.6m }, null!));

        Assert.Equal(2151, (await _manager.GetWeeklyAsync(UserId, null)).TargetCalories);
    }

    [Fact]
    public async Task Racha_HoyVacioYAyerYAnteayerCargados_Dos()
    {
        ConDiasCargados(1, 2);

        Assert.Equal(2, await Racha());
    }

    [Fact]
    public async Task Racha_HoyCargado_CuentaHoy()
    {
        ConDiasCargados(0, 1, 2);

        Assert.Equal(3, await Racha());
    }

    [Fact]
    public async Task Racha_ConUnHueco_SeCorta()
    {
        ConDiasCargados(0, 1, 3, 4, 5);

        Assert.Equal(2, await Racha());
    }

    [Fact]
    public async Task Racha_SinComidasHoyNiAyer_Cero()
    {
        ConDiasCargados(2, 3);

        Assert.Equal(0, await Racha());
    }

    [Fact]
    public async Task Racha_SinComidas_Cero() => Assert.Equal(0, await Racha());

    [Fact]
    public async Task Racha_CuentaDesdeHoyNoDesdeEndDate()
    {
        ConDiasCargados(0, 1, 2, 3, 4, 5, 6, 7, 8, 9);

        var semana = await _manager.GetWeeklyAsync(UserId, Hoy.AddDays(-30));

        Assert.Equal(10, semana.Streak);
    }
}
