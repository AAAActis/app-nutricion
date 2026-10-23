using AppNutricion.Entities.Entities;

namespace AppNutricion.DataAccess.Interfaces;

// userId null = solo alimentos compartidos; con userId, también los personalizados de ese usuario.
public interface IFoodStore
{
    Task<List<Food>> SearchByNameAsync(string normalizedTerm, int? userId, int limit);
    Task<Food?> GetByIdAsync(int id, int? userId);
    Task<Food?> GetByBarcodeAsync(string barcode, int? userId);

    // Favoritos y recientes: siempre de un usuario y solo alimentos que ese usuario puede ver.
    Task<List<Food>> GetFavoritesAsync(int userId);
    Task AddFavoriteAsync(int userId, int foodId);      // idempotente
    Task RemoveFavoriteAsync(int userId, int foodId);   // idempotente
    // Alimentos distintos de sus MealEntries, del cargado más recientemente al más viejo.
    Task<List<Food>> GetRecentAsync(int userId, int limit);

    // Clave (Source, ExternalId). Inserta los nuevos, actualiza solo los que cambiaron, nunca toca los Custom.
    Task<(int Inserted, int Updated)> UpsertSeedAsync(IEnumerable<Food> foods);

    // Personalizados (L8): solo los Custom de ese usuario (los de otro o los compartidos no existen: null/false).
    Task<List<Food>> GetCustomAsync(int userId);    // por nombre
    Task<Food?> GetCustomByIdAsync(int id, int userId);
    Task AddAsync(Food food);
    Task<bool> UpdateCustomAsync(Food food, int userId);
    Task<bool> DeleteCustomAsync(int id, int userId);
    Task<bool> IsInAnyRecipeAsync(int foodId);
}
