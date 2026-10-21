using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.DataAccess.Interfaces;
using Microsoft.Extensions.Logging;

namespace AppNutricion.BusinessLogic.Services;

public class FoodSeedService(IFoodStore foodStore, ILogger<FoodSeedService> logger) : IFoodSeedService
{
    public Task<FoodSeedSummary> SeedAsync(string json) => Task.FromResult(new FoodSeedSummary(0, 0, 0));
}
