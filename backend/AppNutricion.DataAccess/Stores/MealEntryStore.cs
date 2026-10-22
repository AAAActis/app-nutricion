using AppNutricion.DataAccess.DataBaseContext;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;
using Microsoft.EntityFrameworkCore;

namespace AppNutricion.DataAccess.Stores;

public class MealEntryStore(ApplicationDbContext context) : IMealEntryStore
{
    public async Task<List<MealEntry>> GetByDateAsync(int userId, DateOnly date, MealType? mealType)
    {
        var query = OfUser(userId).Where(m => m.Date == date);
        if (mealType is { } type) query = query.Where(m => m.MealType == type);
        var entries = await query.ToListAsync();
        // MealType se guarda como texto: ordenar en la base daría orden alfabético, no el del enum. Un día son pocas filas.
        return entries.OrderBy(m => m.MealType).ThenBy(m => m.CreatedAt).ThenBy(m => m.Id).ToList();
    }

    public Task<MealEntry?> GetByIdAsync(int id, int userId) =>
        OfUser(userId).FirstOrDefaultAsync(m => m.Id == id);

    public async Task AddAsync(IEnumerable<MealEntry> entries)
    {
        context.MealEntries.AddRange(entries);
        await context.SaveChangesAsync();
    }

    public async Task<bool> UpdateAsync(MealEntry entry, int userId) =>
        await OfUser(userId).Where(m => m.Id == entry.Id).ExecuteUpdateAsync(s => s
            .SetProperty(m => m.ItemName, entry.ItemName)
            .SetProperty(m => m.Date, entry.Date)
            .SetProperty(m => m.MealType, entry.MealType)
            .SetProperty(m => m.QuantityGrams, entry.QuantityGrams)
            .SetProperty(m => m.SavedCalories, entry.SavedCalories)
            .SetProperty(m => m.SavedProtein, entry.SavedProtein)
            .SetProperty(m => m.SavedCarbs, entry.SavedCarbs)
            .SetProperty(m => m.SavedFat, entry.SavedFat)) > 0;

    public async Task<bool> DeleteAsync(int id, int userId) =>
        await OfUser(userId).Where(m => m.Id == id).ExecuteDeleteAsync() > 0;

    public Task<Dictionary<MealType, MealTotals>> SumByMealTypeAsync(int userId, DateOnly date) =>
        OfUser(userId).Where(m => m.Date == date).GroupBy(m => m.MealType)
            .Select(g => new
            {
                g.Key,
                Totals = new MealTotals(g.Sum(m => m.SavedCalories), g.Sum(m => m.SavedProtein), g.Sum(m => m.SavedCarbs), g.Sum(m => m.SavedFat), g.Count()),
            })
            .ToDictionaryAsync(x => x.Key, x => x.Totals);

    private IQueryable<MealEntry> OfUser(int userId) =>
        context.MealEntries.AsNoTracking().Where(m => m.UserId == userId);
}
