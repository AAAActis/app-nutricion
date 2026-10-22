using AppNutricion.Entities.Entities;

namespace AppNutricion.BusinessLogic.Interfaces;

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
}
