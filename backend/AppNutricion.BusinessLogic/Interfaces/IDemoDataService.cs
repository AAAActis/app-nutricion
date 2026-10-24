namespace AppNutricion.BusinessLogic.Interfaces;

// Lo que agregó una corrida (en una corrida repetida, todo en 0/false).
public record DemoSeedSummary(bool UserCreated, int Metrics, int Meals, int Workouts, bool GoalCreated, bool RecipeCreated);

// Datos de demo para la presentación (Demo:Enabled). Se cargan con los managers, con las mismas validaciones que la
// API: nunca se escribe directo en la base. Idempotente: cada paso completa solo lo que falta.
public interface IDemoDataService
{
    Task<DemoSeedSummary> SeedAsync(CancellationToken cancellationToken = default);
}
