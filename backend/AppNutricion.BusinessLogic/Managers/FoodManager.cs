using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Helpers;

namespace AppNutricion.BusinessLogic.Managers;

public class FoodManager(IFoodStore foodStore) : IFoodManager
{
    private const int MinTermLength = 2;
    private const int DefaultLimit = 20;
    private const int MaxLimit = 50;

    public async Task<List<Food>> SearchAsync(string? term, int? userId, int? limit)
    {
        var normalized = TextNormalizer.Normalize(term);
        if (normalized.Length < MinTermLength) return [];
        return await foodStore.SearchByNameAsync(normalized, userId, Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit));
    }

    public Task<Food?> GetByIdAsync(int id, int? userId) => foodStore.GetByIdAsync(id, userId);

    public Task<Food?> GetByBarcodeAsync(string? code, int? userId) => Task.FromResult<Food?>(null);

    public decimal ConvertToGrams(Food food, decimal quantity, bool isUnits)
    {
        if (!isUnits) return quantity;
        if (food.PortionWeightGrams is not { } portion)
            throw new ArgumentException($"El alimento '{food.Name}' no tiene porción definida: cargalo en gramos.", nameof(isUnits));
        return quantity * portion;
    }
}
