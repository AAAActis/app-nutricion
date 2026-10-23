using AppNutricion.BusinessLogic.Interfaces;

namespace AppNutricion.BusinessLogic.Services;

public class DemoDataService : IDemoDataService
{
    public const string DemoEmail = "demo@example.com";
    public const string DemoPassword = "demo-nutricion-2026";

    public Task<DemoSeedSummary> SeedAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new DemoSeedSummary(false, 0, 0, 0, false, false));
}
