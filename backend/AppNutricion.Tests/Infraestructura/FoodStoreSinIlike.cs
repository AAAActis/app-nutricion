using AppNutricion.DataAccess.DataBaseContext;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.DataAccess.Stores;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;
using Microsoft.EntityFrameworkCore;

namespace AppNutricion.Tests.Infraestructura;

// FoodStore real salvo SearchByNameAsync: ILIKE es de Postgres y no corre en SQLite. La búsqueda de reemplazo
// respeta lo mismo (compartidos + Custom del usuario, contiene el término) sin el ranking trigram.
public class FoodStoreSinIlike(ApplicationDbContext context) : IFoodStore
{
    private readonly FoodStore _real = new(context);

    public Task<List<Food>> SearchByNameAsync(string normalizedTerm, int? userId, int limit) =>
        context.Foods.AsNoTracking()
            .Where(f => f.Source != FoodSource.Custom || (userId != null && f.CreatedByUserId == userId))
            .Where(f => f.NormalizedName.Contains(normalizedTerm))
            .OrderBy(f => f.NormalizedName.Length).ThenBy(f => f.Id)
            .Take(limit)
            .ToListAsync();

    public Task<Food?> GetByIdAsync(int id, int? userId) => _real.GetByIdAsync(id, userId);
    public Task<Food?> GetByBarcodeAsync(string barcode, int? userId) => _real.GetByBarcodeAsync(barcode, userId);
    public Task<List<Food>> GetFavoritesAsync(int userId) => _real.GetFavoritesAsync(userId);
    public Task AddFavoriteAsync(int userId, int foodId) => _real.AddFavoriteAsync(userId, foodId);
    public Task RemoveFavoriteAsync(int userId, int foodId) => _real.RemoveFavoriteAsync(userId, foodId);
    public Task<List<Food>> GetRecentAsync(int userId, int limit) => _real.GetRecentAsync(userId, limit);
    public Task<(int Inserted, int Updated)> UpsertSeedAsync(IEnumerable<Food> foods) => _real.UpsertSeedAsync(foods);
    public Task<List<Food>> GetCustomAsync(int userId) => _real.GetCustomAsync(userId);
    public Task<Food?> GetCustomByIdAsync(int id, int userId) => _real.GetCustomByIdAsync(id, userId);
    public Task AddAsync(Food food) => _real.AddAsync(food);
    public Task<bool> UpdateCustomAsync(Food food, int userId) => _real.UpdateCustomAsync(food, userId);
    public Task<bool> DeleteCustomAsync(int id, int userId) => _real.DeleteCustomAsync(id, userId);
    public Task<bool> IsInAnyRecipeAsync(int foodId) => _real.IsInAnyRecipeAsync(foodId);
}
