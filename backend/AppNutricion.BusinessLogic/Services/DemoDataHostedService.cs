using AppNutricion.BusinessLogic.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AppNutricion.BusinessLogic.Services;

// Carga los datos de demo al arrancar, solo si Demo:Enabled = true (false en appsettings.json; true solo en
// Development). Se registra después de los seeds de alimentos y rutinas, que usa. Un error se loguea y la API sigue.
public class DemoDataHostedService(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<DemoDataHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Se lee a mano para no sumar el paquete del binder a BusinessLogic (como LlmOptions).
        if (!bool.TryParse(configuration["Demo:Enabled"], out var enabled) || !enabled) return;
        try
        {
            using var scope = scopeFactory.CreateScope();
            var summary = await scope.ServiceProvider.GetRequiredService<IDemoDataService>().SeedAsync(cancellationToken);
            logger.LogInformation(
                "demo: usuario {Email} ({Created}); {Metrics} pesos, {Meals} comidas y {Workouts} entrenamientos agregados",
                DemoDataService.DemoEmail, summary.UserCreated ? "creado" : "ya existía", summary.Metrics, summary.Meals, summary.Workouts);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "demo: falló la carga de los datos de demo");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
