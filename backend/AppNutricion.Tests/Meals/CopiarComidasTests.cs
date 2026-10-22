using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.BusinessLogic.Managers;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;
using Moq;

namespace AppNutricion.Tests.Meals;

public class CopiarComidasTests
{
    private const int UserId = 7;
    private static readonly DateOnly Hoy = DateOnly.FromDateTime(DateTime.UtcNow);
    private static readonly DateOnly Ayer = Hoy.AddDays(-1);

    private readonly Mock<IMealEntryStore> _store = new();
    private readonly MealEntryManager _manager;
    private readonly List<MealEntry> _agregadas = [];

    public CopiarComidasTests()
    {
        _store.Setup(s => s.AddAsync(It.IsAny<IEnumerable<MealEntry>>()))
            .Callback((IEnumerable<MealEntry> e) => _agregadas.AddRange(e));
        _manager = new MealEntryManager(_store.Object, Mock.Of<IFoodManager>(), Mock.Of<IProfileStore>(), TimeProvider.System);
    }

    private static MealEntry Original(int id, MealType tipo, int? foodId) => new()
    {
        Id = id, UserId = UserId, FoodId = foodId, RecipeId = null, ItemName = $"item {id}", Date = Ayer, MealType = tipo,
        QuantityGrams = 120, SavedCalories = 250.5m, SavedProtein = 10.25m, SavedCarbs = 30.1m, SavedFat = 8.75m,
        CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
    };

    [Fact]
    public async Task Copy_TodasLasDelDia_MismosValoresCongeladosEnElDiaDestino()
    {
        var originales = new List<MealEntry> { Original(1, MealType.Breakfast, 3), Original(2, MealType.Dinner, null) };
        _store.Setup(s => s.GetByDateAsync(UserId, Ayer, null)).ReturnsAsync(originales);
        var antes = DateTime.UtcNow;

        var copias = await _manager.CopyAsync(UserId, Ayer, Hoy, null);

        Assert.Equal(2, copias.Count);
        Assert.Equal(copias, _agregadas);
        for (var i = 0; i < copias.Count; i++)
        {
            var (o, c) = (originales[i], copias[i]);
            Assert.NotSame(o, c);
            Assert.Equal(0, c.Id);
            Assert.Equal(UserId, c.UserId);
            Assert.Equal(Hoy, c.Date);
            Assert.Equal(o.FoodId, c.FoodId);
            Assert.Equal(o.RecipeId, c.RecipeId);
            Assert.Equal(o.ItemName, c.ItemName);
            Assert.Equal(o.MealType, c.MealType);
            Assert.Equal(o.QuantityGrams, c.QuantityGrams);
            Assert.Equal(o.SavedCalories, c.SavedCalories);
            Assert.Equal(o.SavedProtein, c.SavedProtein);
            Assert.Equal(o.SavedCarbs, c.SavedCarbs);
            Assert.Equal(o.SavedFat, c.SavedFat);
            Assert.Equal(DateTimeKind.Utc, c.CreatedAt.Kind);
            Assert.InRange(c.CreatedAt, antes, DateTime.UtcNow);
        }
        Assert.Equal(Ayer, originales[0].Date);
    }

    [Fact]
    public async Task Copy_ConMealType_SoloPideEseTipoAlStore()
    {
        _store.Setup(s => s.GetByDateAsync(UserId, Ayer, MealType.Lunch)).ReturnsAsync([Original(1, MealType.Lunch, 3)]);

        var copias = await _manager.CopyAsync(UserId, Ayer, Hoy, MealType.Lunch);

        Assert.Equal(MealType.Lunch, Assert.Single(copias).MealType);
        _store.Verify(s => s.GetByDateAsync(UserId, Ayer, MealType.Lunch), Times.Once);
        _store.Verify(s => s.GetByDateAsync(It.IsAny<int>(), It.IsAny<DateOnly>(), null), Times.Never);
    }

    [Fact]
    public async Task Copy_MismoDia_LanzaArgumentException()
    {
        Assert.Equal("toDate", (await Assert.ThrowsAsync<ArgumentException>(() => _manager.CopyAsync(UserId, Hoy, Hoy, null))).ParamName);
    }

    [Fact]
    public async Task Copy_DiaOrigenVacio_DevuelveVacioSinGuardar()
    {
        _store.Setup(s => s.GetByDateAsync(UserId, Ayer, null)).ReturnsAsync([]);

        var copias = await _manager.CopyAsync(UserId, Ayer, Hoy, null);

        Assert.Empty(copias);
        _store.Verify(s => s.GetByDateAsync(UserId, Ayer, null), Times.Once);
        _store.Verify(s => s.AddAsync(It.IsAny<IEnumerable<MealEntry>>()), Times.Never);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(-800)]
    public async Task Copy_DestinoFueraDeRango_LanzaArgumentException(int diasDesdeHoy)
    {
        Assert.Equal("toDate", (await Assert.ThrowsAsync<ArgumentException>(() => _manager.CopyAsync(UserId, Ayer, Hoy.AddDays(diasDesdeHoy), null))).ParamName);
    }

    [Fact]
    public async Task Copy_MealTypeInexistente_LanzaArgumentException()
    {
        Assert.Equal("mealType", (await Assert.ThrowsAsync<ArgumentException>(() => _manager.CopyAsync(UserId, Ayer, Hoy, (MealType)99))).ParamName);
    }
}
