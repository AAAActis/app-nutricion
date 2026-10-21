using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;

namespace AppNutricion.BusinessLogic.Managers;

public class FoodManager(IFoodStore foodStore) : IFoodManager
{
    public Task<List<Food>> SearchAsync(string? term, int? userId, int? limit) =>
        foodStore.SearchByNameAsync(term ?? "", userId, limit ?? 0);

    public Task<Food?> GetByIdAsync(int id, int? userId) => foodStore.GetByIdAsync(id, userId);

    public decimal ConvertToGrams(Food food, decimal quantity, bool isUnits) => 0;
}
