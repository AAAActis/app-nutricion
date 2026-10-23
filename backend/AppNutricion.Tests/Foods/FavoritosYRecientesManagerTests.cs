using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.BusinessLogic.Managers;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AppNutricion.Tests.Foods;

public class FavoritosYRecientesManagerTests
{
    private const int UserId = 7;

    private readonly Mock<IFoodStore> _store = new();
    private readonly FoodManager _manager;

    public FavoritosYRecientesManagerTests()
    {
        _store.Setup(s => s.GetByIdAsync(3, UserId)).ReturnsAsync(new Food { Id = 3, Name = "Pan" });
        _manager = new FoodManager(_store.Object, Mock.Of<IOpenFoodFactsClient>(), NullLogger<FoodManager>.Instance, TimeProvider.System);
    }

    [Fact]
    public async Task AddFavorite_Visible_LoGuardaConElUserIdRecibido()
    {
        Assert.True(await _manager.AddFavoriteAsync(3, UserId));
        _store.Verify(s => s.AddFavoriteAsync(UserId, 3), Times.Once);
    }

    [Fact]
    public async Task AddFavorite_NoVisible_DevuelveFalseSinGuardar()
    {
        Assert.False(await _manager.AddFavoriteAsync(99, UserId));
        _store.Verify(s => s.GetByIdAsync(99, UserId), Times.Once);
        _store.Verify(s => s.AddFavoriteAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task RemoveFavorite_Visible_LoBorraConElUserIdRecibido()
    {
        Assert.True(await _manager.RemoveFavoriteAsync(3, UserId));
        _store.Verify(s => s.RemoveFavoriteAsync(UserId, 3), Times.Once);
    }

    [Fact]
    public async Task RemoveFavorite_NoVisible_DevuelveFalseSinBorrar()
    {
        Assert.False(await _manager.RemoveFavoriteAsync(99, UserId));
        _store.Verify(s => s.GetByIdAsync(99, UserId), Times.Once);
        _store.Verify(s => s.RemoveFavoriteAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetFavorites_LosPideConElUserIdRecibido()
    {
        var favoritos = new List<Food> { new() { Name = "Pan" } };
        _store.Setup(s => s.GetFavoritesAsync(UserId)).ReturnsAsync(favoritos);

        Assert.Same(favoritos, await _manager.GetFavoritesAsync(UserId));
    }

    [Theory]
    [InlineData(null, 10)]
    [InlineData(5, 5)]
    [InlineData(30, 30)]
    [InlineData(31, 30)]
    [InlineData(0, 1)]
    public async Task GetRecent_LimiteDefault10YMaximo30(int? pedido, int esperado)
    {
        var recientes = new List<Food> { new() { Name = "Pan" } };
        _store.Setup(s => s.GetRecentAsync(UserId, esperado)).ReturnsAsync(recientes);

        Assert.Same(recientes, await _manager.GetRecentAsync(UserId, pedido));
    }
}
