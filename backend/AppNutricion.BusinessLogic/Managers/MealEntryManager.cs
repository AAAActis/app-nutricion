using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;

namespace AppNutricion.BusinessLogic.Managers;

public class MealEntryManager(IMealEntryStore store, IFoodManager foodManager) : IMealEntryManager
{
    public Task<MealEntry?> CreateAsync(int userId, int foodId, decimal quantity, bool isUnits, MealType mealType, DateOnly date, int? recipeId) =>
        Task.FromResult<MealEntry?>(null);

    public Task<MealEntry?> UpdateAsync(int id, int userId, decimal? quantity, bool? isUnits, MealType? mealType, DateOnly? date) =>
        Task.FromResult<MealEntry?>(null);

    public Task<bool> DeleteAsync(int id, int userId) => Task.FromResult(false);

    public Task<List<MealEntry>> ListAsync(int userId, DateOnly? date) => Task.FromResult(new List<MealEntry>());
}
