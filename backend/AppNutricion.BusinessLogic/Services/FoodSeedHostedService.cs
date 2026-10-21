using AppNutricion.BusinessLogic.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AppNutricion.BusinessLogic.Services;

// Carga SeedData/foods_seed.json al arrancar. Un error se loguea y la API sigue levantando.
public class FoodSeedHostedService(IServiceScopeFactory scopeFactory, ILogger<FoodSeedHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "SeedData", "foods_seed.json");
        if (!File.Exists(path))
        {
            logger.LogInformation("foods_seed: no existe {Path}, no se carga nada", path);
            return;
        }
        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken);
            using var scope = scopeFactory.CreateScope();
            var summary = await scope.ServiceProvider.GetRequiredService<IFoodSeedService>().SeedAsync(json);
            logger.LogInformation("foods_seed: {Inserted} insertados, {Updated} actualizados, {Discarded} descartados",
                summary.Inserted, summary.Updated, summary.Discarded);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "foods_seed: falló la carga de {Path}", path);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
