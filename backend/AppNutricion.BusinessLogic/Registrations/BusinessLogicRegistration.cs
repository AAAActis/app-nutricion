using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.BusinessLogic.Managers;
using AppNutricion.BusinessLogic.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AppNutricion.BusinessLogic.Registrations;

public static class BusinessLogicRegistration
{
    public static IServiceCollection AddBusinessLogic(this IServiceCollection services)
    {
        services.AddScoped<IFoodManager, FoodManager>();
        services.AddScoped<IMealEntryManager, MealEntryManager>();
        services.AddScoped<IFoodSeedService, FoodSeedService>();
        services.AddHostedService<FoodSeedHostedService>();
        services.AddHttpClient<IOpenFoodFactsClient, OpenFoodFactsService>(OpenFoodFactsService.Configure);
        return services;
    }
}
