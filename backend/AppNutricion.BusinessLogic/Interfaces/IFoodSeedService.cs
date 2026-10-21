namespace AppNutricion.BusinessLogic.Interfaces;

public record FoodSeedSummary(int Inserted, int Updated, int Discarded);

public interface IFoodSeedService
{
    // json = contenido de foods_seed.json (array de ítems, ver SeedData/README.md).
    Task<FoodSeedSummary> SeedAsync(string json);
}
