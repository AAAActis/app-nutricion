using AppNutricion.DataAccess.DataBaseContext;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;

namespace AppNutricion.DataAccess.Stores;

public class MealEntryStore(ApplicationDbContext context) : IMealEntryStore
{
    public Task<List<MealEntry>> GetByDateAsync(int userId, DateOnly date, MealType? mealType) => Task.FromResult(new List<MealEntry>());

    public Task<MealEntry?> GetByIdAsync(int id, int userId) => Task.FromResult<MealEntry?>(null);

    public Task AddAsync(IEnumerable<MealEntry> entries) => Task.CompletedTask;

    public Task<bool> UpdateAsync(MealEntry entry, int userId) => Task.FromResult(false);

    public Task<bool> DeleteAsync(int id, int userId) => Task.FromResult(false);
}
