using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.BusinessLogic.Managers;
using AppNutricion.BusinessLogic.Options;
using AppNutricion.BusinessLogic.Services;
using AppNutricion.Entities.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace AppNutricion.BusinessLogic.Registrations;

public static class BusinessLogicRegistration
{
    public static IServiceCollection AddBusinessLogic(this IServiceCollection services)
    {
        services.AddScoped<IFoodManager, FoodManager>();
        services.AddScoped<IUserToday, UserToday>();
        services.AddScoped<IMealEntryManager, MealEntryManager>();
        services.AddScoped<IRecipeManager, RecipeManager>();
        services.AddScoped<ISummaryManager, SummaryManager>();
        services.AddScoped<IFoodSeedService, FoodSeedService>();
        services.AddHostedService<FoodSeedHostedService>();
        services.AddHttpClient<IOpenFoodFactsClient, OpenFoodFactsService>(OpenFoodFactsService.Configure);

        // Auth. JwtOptions se configura en Program.cs, que valida Jwt:Key al arrancar.
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddSingleton<IAccessTokenService, JwtAccessTokenService>();
        services.AddScoped<IAuthManager, AuthManager>();

        // Perfil, peso y objetivos (J7/J8).
        services.AddScoped<IProfileManager, ProfileManager>();
        services.AddScoped<IBodyMetricManager, BodyMetricManager>();
        services.AddScoped<IGoalManager, GoalManager>();

        // Cuenta: borrado y exportación (J5).
        services.AddScoped<IAccountManager, AccountManager>();

        // Asistente (S9/S10). El modelo sale de Llm:Provider; sin clave, el Fake.
        services.AddSingleton(sp => LlmOptions.From(sp.GetRequiredService<IConfiguration>()));
        services.AddSingleton(sp => ChatModelFactory.Create(sp.GetRequiredService<LlmOptions>(), sp.GetRequiredService<ILogger<ChatModelFactory>>(),
            () => ActivatorUtilities.CreateInstance<OpenAiCompatibleChatModel>(sp)));
        services.AddHttpClient(OpenAiCompatibleChatModel.HttpClientName);
        services.AddScoped<IRoutineSummaryProvider, RoutineSummaryProvider>();
        services.AddScoped<AssistantTools>();
        services.AddScoped<IAssistantManager, AssistantManager>();
        // Rutinas y entrenamientos (S7).
        services.AddScoped<IRoutineManager, RoutineManager>();
        services.AddScoped<IWorkoutManager, WorkoutManager>();
        services.AddScoped<ITrainingSeedService, TrainingSeedService>();
        services.AddHostedService<TrainingSeedHostedService>();
        // Datos de demo (solo con Demo:Enabled = true), después de los seeds que usa.
        services.AddScoped<IDemoDataService, DemoDataService>();
        services.AddHostedService<DemoDataHostedService>();
        return services;
    }
}
