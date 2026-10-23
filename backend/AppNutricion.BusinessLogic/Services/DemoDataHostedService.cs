using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AppNutricion.BusinessLogic.Services;

public class DemoDataHostedService(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<DemoDataHostedService> logger) : IHostedService
{
    public IServiceScopeFactory ScopeFactory { get; } = scopeFactory;
    public IConfiguration Configuration { get; } = configuration;
    public ILogger Logger { get; } = logger;

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
