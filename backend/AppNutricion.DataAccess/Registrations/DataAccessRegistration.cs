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
        services.AddScoped<IMealEntryStore, MealEntryStore>();
        services.AddScoped<IRecipeStore, RecipeStore>();
        services.AddScoped<IUserAccountStore, UserAccountStore>();
        services.AddScoped<IProfileStore, ProfileStore>();
        services.AddScoped<IBodyMetricStore, BodyMetricStore>();
        services.AddScoped<IGoalStore, GoalStore>();
        services.AddScoped<IExportStore, ExportStore>();
        services.AddScoped<IAssistantStore, AssistantStore>();
        services.AddScoped<IExerciseStore, ExerciseStore>();
        services.AddScoped<IRoutineStore, RoutineStore>();
        services.AddScoped<IWorkoutStore, WorkoutStore>();
        return services;
    }
}
