using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.BusinessLogic.Validators;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Helpers;
using Microsoft.Extensions.Logging;

namespace AppNutricion.BusinessLogic.Managers;

public class FoodManager(IFoodStore foodStore, IOpenFoodFactsClient openFoodFacts, ILogger<FoodManager> logger) : IFoodManager
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

    public Task<Food?> GetByBarcodeAsync(string? code, int? userId)
    {
        if (code is not { Length: >= 8 and <= 14 } || !code.All(char.IsAsciiDigit))
            throw new ArgumentException("El código de barras debe tener entre 8 y 14 dígitos.", nameof(code));
        return GetByValidBarcodeAsync(code, userId);
    }

    // Si no está en nuestra base, se busca en Open Food Facts y se guarda (Source = OpenFoodFacts, ExternalId = código).
    private async Task<Food?> GetByValidBarcodeAsync(string code, int? userId)
    {
        var food = await foodStore.GetByBarcodeAsync(code, userId);
        if (food is not null) return food;

        var external = await openFoodFacts.GetByBarcodeAsync(code);
        if (external is null) return null;
        if (FoodValidator.GetInvalidReason(external) is { } reason)
        {
            logger.LogWarning("Open Food Facts {Barcode} descartado: {Reason}", code, reason);
            return null;
        }
        external.NormalizedName = TextNormalizer.Normalize(external.Name);
        external.CreatedAt = DateTime.UtcNow;
        // shortcut: dos pedidos simultáneos del mismo código nuevo chocan en el índice único (500); agregar reintento si pasa.
        await foodStore.UpsertSeedAsync([external]);
        return external;
    }

    public decimal ConvertToGrams(Food food, decimal quantity, bool isUnits)
    {
        if (!isUnits) return quantity;
        if (food.PortionWeightGrams is not { } portion)
            throw new ArgumentException($"El alimento '{food.Name}' no tiene porción definida: cargalo en gramos.", nameof(isUnits));
        return quantity * portion;
    }
}
