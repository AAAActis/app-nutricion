using AppNutricion.DataAccess.DataBaseContext;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using Microsoft.EntityFrameworkCore;

namespace AppNutricion.DataAccess.Stores;

public class RecipeStore(ApplicationDbContext context) : IRecipeStore
{
    public Task<List<Recipe>> GetByUserAsync(int userId) =>
        WithIngredients(userId).OrderBy(r => r.Name).ThenBy(r => r.Id).ToListAsync();

    public Task<Recipe?> GetByIdAsync(int id, int userId) =>
        WithIngredients(userId).FirstOrDefaultAsync(r => r.Id == id);

    public async Task AddAsync(Recipe recipe)
    {
        context.Recipes.Add(recipe);
        await context.SaveChangesAsync();
    }

    // Los ingredientes que salen de la colección se borran (huérfanos de una relación requerida). Un solo SaveChanges: todo o nada.
    public async Task<bool> UpdateAsync(Recipe recipe, int userId)
    {
        var current = await context.Recipes.Include(r => r.Ingredients).FirstOrDefaultAsync(r => r.Id == recipe.Id && r.UserId == userId);
        if (current is null) return false;
        current.Name = recipe.Name;
        current.Servings = recipe.Servings;
        current.Ingredients.Clear();
        foreach (var ingredient in recipe.Ingredients)
            current.Ingredients.Add(new RecipeIngredient { FoodId = ingredient.FoodId, QuantityGrams = ingredient.QuantityGrams });
        await context.SaveChangesAsync();
        return true;
    }

    // Los ingredientes caen en cascada; las MealEntries quedan con RecipeId null (SetNull).
    public async Task<bool> DeleteAsync(int id, int userId) =>
        await context.Recipes.Where(r => r.Id == id && r.UserId == userId).ExecuteDeleteAsync() > 0;

    private IQueryable<Recipe> WithIngredients(int userId) =>
        context.Recipes.AsNoTracking().Where(r => r.UserId == userId)
            .Include(r => r.Ingredients.OrderBy(i => i.Id)).ThenInclude(i => i.Food)
            .AsSplitQuery();
}
