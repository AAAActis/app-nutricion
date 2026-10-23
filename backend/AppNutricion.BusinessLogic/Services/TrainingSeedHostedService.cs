using AppNutricion.BusinessLogic.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AppNutricion.BusinessLogic.Services;

// Carga SeedData/exercises_seed.json al arrancar (mismo patrón que FoodSeedHostedService). Un error se loguea y la API sigue.
public class TrainingSeedHostedService(IServiceScopeFactory scopeFactory, ILogger<TrainingSeedHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "SeedData", "exercises_seed.json");
        if (!File.Exists(path))
        {
            logger.LogInformation("exercises_seed: no existe {Path}, no se carga nada", path);
            return;
        }
        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken);
            using var scope = scopeFactory.CreateScope();
            var summary = await scope.ServiceProvider.GetRequiredService<ITrainingSeedService>().SeedAsync(json);
            logger.LogInformation("exercises_seed: {Exercises} ejercicios y {Routines} rutinas precargadas insertados",
                summary.ExercisesInserted, summary.RoutinesInserted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "exercises_seed: falló la carga de {Path}", path);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
