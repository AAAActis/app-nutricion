using AppNutricion.DataAccess.DataBaseContext;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;

namespace AppNutricion.DataAccess.Stores;

public class RecipeStore(ApplicationDbContext context) : IRecipeStore
{
    public Task<List<Recipe>> GetByUserAsync(int userId) => throw new NotImplementedException();

    public Task<Recipe?> GetByIdAsync(int id, int userId) => throw new NotImplementedException();

    public Task AddAsync(Recipe recipe) => throw new NotImplementedException();

    public Task<bool> UpdateAsync(Recipe recipe, int userId) => throw new NotImplementedException();

    public Task<bool> DeleteAsync(int id, int userId) => throw new NotImplementedException();
}
