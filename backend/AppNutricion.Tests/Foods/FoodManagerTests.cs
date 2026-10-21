using AppNutricion.BusinessLogic.Managers;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using Moq;

namespace AppNutricion.Tests.Foods;

public class FoodManagerTests
{
    private readonly Mock<IFoodStore> _store = new();
    private readonly FoodManager _manager;

    public FoodManagerTests()
    {
        _store.Setup(s => s.SearchByNameAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int>()))
            .ReturnsAsync([new Food { Name = "Café" }]);
        _manager = new FoodManager(_store.Object);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  a  ")]
    [InlineData("á")]
    public async Task SearchAsync_TerminoCorto_DevuelveVacioSinLlamarAlStore(string? term)
    {
        var result = await _manager.SearchAsync(term, null, null);

        Assert.Empty(result);
        _store.Verify(s => s.SearchByNameAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task SearchAsync_TerminoConTildes_ElStoreRecibeElTerminoNormalizado()
    {
        await _manager.SearchAsync("  Café con LECHE ", 7, null);

        _store.Verify(s => s.SearchByNameAsync("cafe con leche", 7, It.IsAny<int>()), Times.Once);
    }

    [Theory]
    [InlineData(null, 20)]
    [InlineData(10, 10)]
    [InlineData(80, 50)]
    [InlineData(0, 1)]
    public async Task SearchAsync_Limite_PorDefecto20YRecortadoEntre1Y50(int? limit, int expected)
    {
        await _manager.SearchAsync("leche", null, limit);

        _store.Verify(s => s.SearchByNameAsync("leche", null, expected), Times.Once);
    }

    [Fact]
    public void ConvertToGrams_EnGramos_DevuelveLaCantidad()
    {
        var food = new Food { Name = "Arroz", PortionWeightGrams = 80m };

        Assert.Equal(150m, _manager.ConvertToGrams(food, 150m, isUnits: false));
    }

    [Fact]
    public void ConvertToGrams_EnUnidades_MultiplicaPorLaPorcion()
    {
        var food = new Food { Name = "Huevo", PortionWeightGrams = 50m };

        Assert.Equal(125m, _manager.ConvertToGrams(food, 2.5m, isUnits: true));
    }

    [Fact]
    public void ConvertToGrams_EnUnidadesSinPorcion_LanzaArgumentException()
    {
        var food = new Food { Name = "Harina" };

        var ex = Assert.Throws<ArgumentException>(() => _manager.ConvertToGrams(food, 2m, isUnits: true));
        Assert.Contains("Harina", ex.Message);
    }
}
