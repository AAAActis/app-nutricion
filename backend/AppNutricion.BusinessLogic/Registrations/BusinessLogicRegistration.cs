using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.BusinessLogic.Managers;
using Microsoft.Extensions.DependencyInjection;

namespace AppNutricion.BusinessLogic.Registrations;

public static class BusinessLogicRegistration
{
    public static IServiceCollection AddBusinessLogic(this IServiceCollection services)
    {
        services.AddScoped<IFoodManager, FoodManager>();
        return services;
    }
}
