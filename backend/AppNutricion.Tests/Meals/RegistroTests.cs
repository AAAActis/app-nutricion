using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.BusinessLogic.Managers;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.DataAccess.Stores;
using AppNutricion.Tests.Infraestructura;
using Microsoft.Extensions.DependencyInjection;

namespace AppNutricion.Tests.Meals;

public class RegistroTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public void ManagerYStoreDeComidas_EstanRegistrados()
    {
        using var scope = factory.Services.CreateScope();

        Assert.IsType<MealEntryManager>(scope.ServiceProvider.GetService<IMealEntryManager>());
        Assert.IsType<MealEntryStore>(scope.ServiceProvider.GetService<IMealEntryStore>());
    }
}
