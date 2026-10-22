using AppNutricion.DataAccess.DataBaseContext;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;
using Microsoft.EntityFrameworkCore;

namespace AppNutricion.DataAccess.Stores;

public class FoodStore(ApplicationDbContext context) : IFoodStore
{
    public Task<List<Food>> SearchByNameAsync(string normalizedTerm, int? userId, int limit)
    {
        // % y _ del usuario se escapan para que no funcionen como comodines del LIKE.
        var pattern = "%" + normalizedTerm.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
        return Visible(userId)
            .Where(f => EF.Functions.ILike(f.NormalizedName, pattern, "\\"))
            .OrderBy(f => f.NormalizedName == normalizedTerm ? 0 : f.NormalizedName.StartsWith(normalizedTerm) ? 1 : 2)
            .ThenBy(f => f.NormalizedName.Length)
            .ThenBy(f => f.NormalizedName)
            .Take(limit)
            .ToListAsync();
    }

    public Task<Food?> GetByIdAsync(int id, int? userId) =>
        Visible(userId).FirstOrDefaultAsync(f => f.Id == id);

    public Task<Food?> GetByBarcodeAsync(string barcode, int? userId) =>
        Visible(userId).Where(f => f.Barcode == barcode).OrderBy(f => f.Id).FirstOrDefaultAsync();

    public Task<List<Food>> GetFavoritesAsync(int userId) => Task.FromResult(new List<Food>());

    public Task AddFavoriteAsync(int userId, int foodId) => Task.CompletedTask;

    public Task RemoveFavoriteAsync(int userId, int foodId) => Task.CompletedTask;

    public Task<List<Food>> GetRecentAsync(int userId, int limit) => Task.FromResult(new List<Food>());

    public async Task<(int Inserted, int Updated)> UpsertSeedAsync(IEnumerable<Food> foods)
    {
        var items = foods.Where(f => f.Source != FoodSource.Custom && f.ExternalId != null).ToList();
        var externalIds = items.Select(f => f.ExternalId).Distinct().ToList();
        var existing = await context.Foods
            .Where(f => f.Source != FoodSource.Custom && externalIds.Contains(f.ExternalId))
            .ToListAsync();
        var byKey = existing.ToDictionary(f => (f.Source, f.ExternalId!));

        var inserted = 0;
        foreach (var food in items)
        {
            if (!byKey.TryGetValue((food.Source, food.ExternalId!), out var current))
            {
                context.Foods.Add(food);
                byKey[(food.Source, food.ExternalId!)] = food;
                inserted++;
                continue;
            }
            current.Name = food.Name;
            current.NormalizedName = food.NormalizedName;
            current.Brand = food.Brand;
            current.Barcode = food.Barcode;
            current.CaloriesPer100g = food.CaloriesPer100g;
            current.ProteinPer100g = food.ProteinPer100g;
            current.CarbsPer100g = food.CarbsPer100g;
            current.FatPer100g = food.FatPer100g;
            current.PortionName = food.PortionName;
            current.PortionWeightGrams = food.PortionWeightGrams;
        }

        // Asignar el mismo valor no marca la entidad como modificada: solo se cuentan (y se guardan) las que cambiaron.
        context.ChangeTracker.DetectChanges();
        var updated = existing.Count(f => context.Entry(f).State == EntityState.Modified);
        await context.SaveChangesAsync();
        return (inserted, updated);
    }

    private IQueryable<Food> Visible(int? userId) =>
        context.Foods.AsNoTracking().Where(f => f.CreatedByUserId == null || f.CreatedByUserId == userId);
}
