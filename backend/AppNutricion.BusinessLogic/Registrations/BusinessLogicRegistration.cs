using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.BusinessLogic.Managers;
using AppNutricion.BusinessLogic.Services;
using AppNutricion.Entities.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AppNutricion.BusinessLogic.Registrations;

public static class BusinessLogicRegistration
{
    public static IServiceCollection AddBusinessLogic(this IServiceCollection services)
    {
        services.AddScoped<IFoodManager, FoodManager>();
        services.AddScoped<IFoodSeedService, FoodSeedService>();
        services.AddHostedService<FoodSeedHostedService>();
        services.AddHttpClient<IOpenFoodFactsClient, OpenFoodFactsService>(OpenFoodFactsService.Configure);

        // Auth. JwtOptions se configura en Program.cs, que valida Jwt:Key al arrancar.
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddSingleton<IAccessTokenService, JwtAccessTokenService>();
        services.AddScoped<IAuthManager, AuthManager>();
        return services;
    }
}
