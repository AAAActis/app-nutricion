using AppNutricion.BusinessLogic.Services;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace AppNutricion.Tests.Meals;

// Reloj: 22/10/2026 02:30 UTC = 21/10/2026 23:30 en Córdoba (UTC−3). Las reglas de rango del diario
// se prueban a través de las comidas en FechasDeComidasTests.
public class UserTodayTests
{
    private const int UserId = 7;
    private readonly FakeTimeProvider _reloj = new(new DateTimeOffset(2026, 10, 22, 2, 30, 0, TimeSpan.Zero));
    private readonly Mock<IProfileStore> _perfiles = new();

    private Task<DateOnly> Hoy() => new UserToday(_perfiles.Object, _reloj).GetAsync(UserId);

    private void ConZona(string zona) =>
        _perfiles.Setup(p => p.GetByUserIdAsync(UserId)).ReturnsAsync(new Profile { UserId = UserId, TimeZoneId = zona });

    [Fact]
    public async Task ConPerfil_EsElDiaLocal()
    {
        ConZona("America/Argentina/Cordoba");

        Assert.Equal(new DateOnly(2026, 10, 21), await Hoy());
    }

    [Fact]
    public async Task SinPerfil_EsElDiaUtc() => Assert.Equal(new DateOnly(2026, 10, 22), await Hoy());

    [Fact]
    public async Task ConZonaQueNoEsIana_EsElDiaUtc()
    {
        ConZona("Argentina Standard Time");

        Assert.Equal(new DateOnly(2026, 10, 22), await Hoy());
    }
}
