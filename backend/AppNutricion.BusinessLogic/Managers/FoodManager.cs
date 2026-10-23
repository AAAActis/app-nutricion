using AppNutricion.BusinessLogic.Exceptions;
using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.BusinessLogic.Validators;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;
using AppNutricion.Entities.Helpers;
using Microsoft.Extensions.Logging;

namespace AppNutricion.BusinessLogic.Managers;

public class FoodManager(IFoodStore foodStore, IOpenFoodFactsClient openFoodFacts, ILogger<FoodManager> logger, TimeProvider timeProvider) : IFoodManager
{
    private const int MinTermLength = 2;
    private const int DefaultLimit = 20;
    private const int MaxLimit = 50;
    private const int DefaultRecentLimit = 10;
    private const int MaxRecentLimit = 30;
    private const int MaxCustomNameLength = 120;
    private const int MaxBrandLength = 120;         // largos de FoodConfiguration
    private const int MaxPortionNameLength = 60;
    private const decimal MaxPortionGrams = 5000;

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

    public async Task<bool> AddFavoriteAsync(int foodId, int userId)
    {
        if (await foodStore.GetByIdAsync(foodId, userId) is null) return false;
        await foodStore.AddFavoriteAsync(userId, foodId);
        return true;
    }

    public async Task<bool> RemoveFavoriteAsync(int foodId, int userId)
    {
        if (await foodStore.GetByIdAsync(foodId, userId) is null) return false;
        await foodStore.RemoveFavoriteAsync(userId, foodId);
        return true;
    }

    public Task<List<Food>> GetFavoritesAsync(int userId) => foodStore.GetFavoritesAsync(userId);

    public Task<List<Food>> GetRecentAsync(int userId, int? limit) =>
        foodStore.GetRecentAsync(userId, Math.Clamp(limit ?? DefaultRecentLimit, 1, MaxRecentLimit));

    public async Task<Food> CreateCustomAsync(int userId, CustomFoodData data)
    {
        var food = new Food { Source = FoodSource.Custom, CreatedByUserId = userId, CreatedAt = timeProvider.GetUtcNow().UtcDateTime };
        ApplyCustom(food, data);
        await foodStore.AddAsync(food);
        return food;
    }

    public Task<List<Food>> GetCustomAsync(int userId) => foodStore.GetCustomAsync(userId);

    public Task<Food?> GetCustomByIdAsync(int id, int userId) => foodStore.GetCustomByIdAsync(id, userId);

    // Las comidas ya cargadas no cambian: tienen sus valores congelados.
    public async Task<Food?> UpdateCustomAsync(int id, int userId, CustomFoodData data)
    {
        var food = await foodStore.GetCustomByIdAsync(id, userId);
        if (food is null) return null;
        ApplyCustom(food, data);
        return await foodStore.UpdateCustomAsync(food, userId) ? food : null;
    }

    public async Task<bool> DeleteCustomAsync(int id, int userId)
    {
        if (await foodStore.GetCustomByIdAsync(id, userId) is null) return false;
        // RecipeIngredients.FoodId es NoAction: borrarlo haría fallar la FK.
        if (await foodStore.IsInAnyRecipeAsync(id))
            throw new ConflictException("El alimento está en una receta: sacalo de la receta antes de borrarlo");
        return await foodStore.DeleteCustomAsync(id, userId);
    }

    // Valida (ArgumentException con el campo) y copia los datos del usuario. Mismos topes de macros que FoodValidator.
    private static void ApplyCustom(Food food, CustomFoodData data)
    {
        var name = data.Name?.Trim();
        if (name is not { Length: >= 1 and <= MaxCustomNameLength })
            throw new ArgumentException($"El nombre tiene que tener entre 1 y {MaxCustomNameLength} caracteres.", "name");
        var brand = string.IsNullOrWhiteSpace(data.Brand) ? null : data.Brand.Trim();
        if (brand?.Length > MaxBrandLength) throw new ArgumentException($"La marca es de hasta {MaxBrandLength} caracteres.", "brand");
        var kcal = PerHundredGrams(data.CaloriesPer100g, "caloriesPer100g");
        if (kcal > FoodValidator.MaxCaloriesPer100g)
            throw new ArgumentException($"Las kcal por 100 g van de 0 a {FoodValidator.MaxCaloriesPer100g}.", "caloriesPer100g");
        var protein = PerHundredGrams(data.ProteinPer100g, "proteinPer100g");
        var carbs = PerHundredGrams(data.CarbsPer100g, "carbsPer100g");
        var fat = PerHundredGrams(data.FatPer100g, "fatPer100g");
        if (protein + carbs + fat > FoodValidator.MaxMacrosPer100g)
            throw new ArgumentException($"Proteína + carbohidratos + grasas no pueden sumar más de {FoodValidator.MaxMacrosPer100g} g por 100 g.", "fatPer100g");

        var portionName = string.IsNullOrWhiteSpace(data.PortionName) ? null : data.PortionName.Trim();
        if (portionName is not null && data.PortionWeightGrams is null)
            throw new ArgumentException("Con nombre de porción hay que mandar cuántos gramos pesa.", "portionWeightGrams");
        if (portionName is null && data.PortionWeightGrams is not null)
            throw new ArgumentException("Con gramos de porción hay que mandar el nombre de la porción.", "portionName");
        if (portionName?.Length > MaxPortionNameLength)
            throw new ArgumentException($"El nombre de la porción es de hasta {MaxPortionNameLength} caracteres.", "portionName");
        if (data.PortionWeightGrams is <= 0 or > MaxPortionGrams)
            throw new ArgumentException($"La porción tiene que pesar más de 0 y hasta {MaxPortionGrams} g.", "portionWeightGrams");

        food.Name = name;
        food.NormalizedName = TextNormalizer.Normalize(name);
        food.Brand = brand;
        (food.CaloriesPer100g, food.ProteinPer100g, food.CarbsPer100g, food.FatPer100g) = (kcal, protein, carbs, fat);
        (food.PortionName, food.PortionWeightGrams) = (portionName, data.PortionWeightGrams);
    }

    private static decimal PerHundredGrams(decimal? value, string field) => value switch
    {
        null => throw new ArgumentException("Falta el valor por 100 g.", field),
        < 0 => throw new ArgumentException("El valor por 100 g no puede ser negativo.", field),
        _ => value.Value,
    };

    public decimal ConvertToGrams(Food food, decimal quantity, bool isUnits)
    {
        if (!isUnits) return quantity;
        if (food.PortionWeightGrams is not { } portion)
            throw new ArgumentException($"El alimento '{food.Name}' no tiene porción definida: cargalo en gramos.", nameof(isUnits));
        return quantity * portion;
    }
}
