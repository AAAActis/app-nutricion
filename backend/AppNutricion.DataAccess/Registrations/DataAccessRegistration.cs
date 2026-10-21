using AppNutricion.DataAccess.DataBaseContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppNutricion.DataAccess.Registrations;

public static class DataAccessRegistration
{
    public static IServiceCollection AddDataAccess(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));
        // Acá se registran los Stores a medida que aparezcan: services.AddScoped<IFoodStore, FoodStore>();
        return services;
    }
}
