using AppNutricion.Entities.Entities;

namespace AppNutricion.BusinessLogic.Interfaces;

public interface IFoodManager
{
    Task<List<Food>> SearchAsync(string? term, int? userId, int? limit);
    Task<Food?> GetByIdAsync(int id, int? userId);
    decimal ConvertToGrams(Food food, decimal quantity, bool isUnits);
}
