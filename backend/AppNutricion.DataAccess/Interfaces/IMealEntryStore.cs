using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;

namespace AppNutricion.DataAccess.Interfaces;

// Todo filtra por userId: una entrada de otro usuario no existe (null/false).
public interface IMealEntryStore
{
    // Ordenadas por MealType (orden del enum) y después por CreatedAt.
    Task<List<MealEntry>> GetByDateAsync(int userId, DateOnly date, MealType? mealType);
    Task<MealEntry?> GetByIdAsync(int id, int userId);
    Task AddAsync(IEnumerable<MealEntry> entries);
    Task<bool> UpdateAsync(MealEntry entry, int userId);
    Task<bool> DeleteAsync(int id, int userId);
}
