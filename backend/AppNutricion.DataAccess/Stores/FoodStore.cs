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

    public Task<List<Food>> GetFavoritesAsync(int userId) =>
        context.FavoriteFoods.Where(f => f.UserId == userId)
            .Join(Visible(userId), fav => fav.FoodId, food => food.Id, (fav, food) => new { fav.CreatedAt, Food = food })
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => x.Food)
            .ToListAsync();

    public async Task AddFavoriteAsync(int userId, int foodId)
    {
        if (await context.FavoriteFoods.AnyAsync(f => f.UserId == userId && f.FoodId == foodId)) return;
        var favorite = context.FavoriteFoods.Add(new FavoriteFood { UserId = userId, FoodId = foodId, CreatedAt = DateTime.UtcNow });
        try
        {
            await context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Dos POST simultáneos: el otro lo agregó entre el AnyAsync y el insert y este choca en la PK
            // (23505 en Postgres, constraint en SQLite). Si la fila ya está, "ya existía"; si no (FK u otro), es error.
            favorite.State = EntityState.Detached;
            if (!await context.FavoriteFoods.AnyAsync(f => f.UserId == userId && f.FoodId == foodId)) throw;
        }
    }

    public Task RemoveFavoriteAsync(int userId, int foodId) =>
        context.FavoriteFoods.Where(f => f.UserId == userId && f.FoodId == foodId).ExecuteDeleteAsync();

    public Task<List<Food>> GetRecentAsync(int userId, int limit) =>
        context.MealEntries.Where(m => m.UserId == userId && m.FoodId != null)
            .GroupBy(m => m.FoodId!.Value)
            .Select(g => new { FoodId = g.Key, LastUsed = g.Max(m => m.CreatedAt) })
            .Join(Visible(userId), r => r.FoodId, food => food.Id, (r, food) => new { r.LastUsed, Food = food })
            .OrderByDescending(x => x.LastUsed)
            .ThenBy(x => x.Food.Id)
            .Take(limit)
            .Select(x => x.Food)
            .ToListAsync();

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

    public Task<List<Food>> GetCustomAsync(int userId) =>
        CustomOf(userId).OrderBy(f => f.NormalizedName).ThenBy(f => f.Id).ToListAsync();

    public Task<Food?> GetCustomByIdAsync(int id, int userId) =>
        CustomOf(userId).FirstOrDefaultAsync(f => f.Id == id);

    public async Task AddAsync(Food food)
    {
        context.Foods.Add(food);
        await context.SaveChangesAsync();
    }

    // Source, dueño y CreatedAt no se tocan.
    public async Task<bool> UpdateCustomAsync(Food food, int userId) =>
        await CustomOf(userId).Where(f => f.Id == food.Id).ExecuteUpdateAsync(s => s
            .SetProperty(f => f.Name, food.Name)
            .SetProperty(f => f.NormalizedName, food.NormalizedName)
            .SetProperty(f => f.Brand, food.Brand)
            .SetProperty(f => f.CaloriesPer100g, food.CaloriesPer100g)
            .SetProperty(f => f.ProteinPer100g, food.ProteinPer100g)
            .SetProperty(f => f.CarbsPer100g, food.CarbsPer100g)
            .SetProperty(f => f.FatPer100g, food.FatPer100g)
            .SetProperty(f => f.PortionName, food.PortionName)
            .SetProperty(f => f.PortionWeightGrams, food.PortionWeightGrams)) > 0;

    // Las MealEntries quedan con FoodId null (SetNull); favoritos y sugerencias caen en cascada.
    public async Task<bool> DeleteCustomAsync(int id, int userId) =>
        await CustomOf(userId).Where(f => f.Id == id).ExecuteDeleteAsync() > 0;

    public Task<bool> IsInAnyRecipeAsync(int foodId) =>
        context.RecipeIngredients.AnyAsync(i => i.FoodId == foodId);

    private IQueryable<Food> CustomOf(int userId) =>
        context.Foods.AsNoTracking().Where(f => f.Source == FoodSource.Custom && f.CreatedByUserId == userId);

    private IQueryable<Food> Visible(int? userId) =>
        context.Foods.AsNoTracking().Where(f => f.CreatedByUserId == null || f.CreatedByUserId == userId);
}
