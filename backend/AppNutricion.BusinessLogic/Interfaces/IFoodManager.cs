using AppNutricion.Entities.Entities;

namespace AppNutricion.BusinessLogic.Interfaces;

// Alimento personalizado (L8): valores por 100 g (obligatorios: null → 400); la porción es opcional, pero nombre y gramos van juntos.
public record CustomFoodData(string? Name, string? Brand, decimal? CaloriesPer100g, decimal? ProteinPer100g, decimal? CarbsPer100g,
    decimal? FatPer100g, string? PortionName, decimal? PortionWeightGrams);

public interface IFoodManager
{
    Task<List<Food>> SearchAsync(string? term, int? userId, int? limit);
    Task<Food?> GetByIdAsync(int id, int? userId);
    Task<Food?> GetByBarcodeAsync(string? code, int? userId);
    decimal ConvertToGrams(Food food, decimal quantity, bool isUnits);

    // false = el alimento no existe o el usuario no lo ve.
    Task<bool> AddFavoriteAsync(int foodId, int userId);
    Task<bool> RemoveFavoriteAsync(int foodId, int userId);
    Task<List<Food>> GetFavoritesAsync(int userId);
    Task<List<Food>> GetRecentAsync(int userId, int? limit);

    // Personalizados (L8): solo los Custom del usuario. Dato inválido → ArgumentException con el campo.
    // De otro usuario o no Custom → null/false. Borrar uno que está en una receta → ConflictException.
    Task<Food> CreateCustomAsync(int userId, CustomFoodData data);
    Task<List<Food>> GetCustomAsync(int userId);
    Task<Food?> GetCustomByIdAsync(int id, int userId);
    Task<Food?> UpdateCustomAsync(int id, int userId, CustomFoodData data);
    Task<bool> DeleteCustomAsync(int id, int userId);
}
