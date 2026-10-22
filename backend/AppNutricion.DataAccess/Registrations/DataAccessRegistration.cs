using AppNutricion.DataAccess.DataBaseContext;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.DataAccess.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppNutricion.DataAccess.Registrations;

public static class DataAccessRegistration
{
    public static IServiceCollection AddDataAccess(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IFoodStore, FoodStore>();
        services.AddScoped<IUserAccountStore, UserAccountStore>();
        services.AddScoped<IProfileStore, ProfileStore>();
        services.AddScoped<IBodyMetricStore, BodyMetricStore>();
        services.AddScoped<IGoalStore, GoalStore>();
        return services;
    }
}
