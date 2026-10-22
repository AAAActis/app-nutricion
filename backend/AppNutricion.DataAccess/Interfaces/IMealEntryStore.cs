using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;

namespace AppNutricion.DataAccess.Interfaces;

// Sumas de los valores congelados (Saved*) y cantidad de entradas.
public record MealTotals(decimal Calories, decimal Protein, decimal Carbs, decimal Fat, int Entries);

// Todo filtra por userId: una entrada de otro usuario no existe (null/false).
public interface IMealEntryStore
{
    // Ordenadas por MealType (orden del enum) y después por CreatedAt.
    Task<List<MealEntry>> GetByDateAsync(int userId, DateOnly date, MealType? mealType);
    Task<MealEntry?> GetByIdAsync(int id, int userId);
    Task AddAsync(IEnumerable<MealEntry> entries);
    Task<bool> UpdateAsync(MealEntry entry, int userId);
    Task<bool> DeleteAsync(int id, int userId);

    // Sumado en la base, un grupo por tipo de comida con entradas ese día.
    Task<Dictionary<MealType, MealTotals>> SumByMealTypeAsync(int userId, DateOnly date);

    // Sumado en la base, un grupo por día con entradas entre from y to (incluidos).
    Task<Dictionary<DateOnly, MealTotals>> SumByDateAsync(int userId, DateOnly from, DateOnly to);

    // Días distintos con entradas hasta upTo (incluido), del más reciente al más viejo, como mucho max.
    Task<List<DateOnly>> GetLoggedDatesAsync(int userId, DateOnly upTo, int max);
}
