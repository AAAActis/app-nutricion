using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;

namespace AppNutricion.BusinessLogic.Interfaces;

// Dato inválido → ArgumentException. No encontrado (o de otro usuario) → null/false.
public interface IMealEntryManager
{
    Task<MealEntry?> CreateAsync(int userId, int foodId, decimal quantity, bool isUnits, MealType mealType, DateOnly date, int? recipeId);
    Task<MealEntry?> UpdateAsync(int id, int userId, decimal? quantity, bool? isUnits, MealType? mealType, DateOnly? date);
    Task<bool> DeleteAsync(int id, int userId);
    Task<List<MealEntry>> ListAsync(int userId, DateOnly? date);
    // Copia las entradas de fromDate a toDate (todas o solo un tipo) con los mismos valores congelados.
    Task<List<MealEntry>> CopyAsync(int userId, DateOnly fromDate, DateOnly toDate, MealType? mealType);
    // Historial entre from y to (obligatorios, from <= to, hasta 366 días) como CSV (RFC 4180, sin BOM).
    Task<string> ExportCsvAsync(int userId, DateOnly? from, DateOnly? to);
}
