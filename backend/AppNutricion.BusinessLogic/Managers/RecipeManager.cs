using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.DataAccess.Interfaces;

namespace AppNutricion.BusinessLogic.Managers;

public class RecipeManager(IRecipeStore store, IFoodManager foodManager, TimeProvider timeProvider) : IRecipeManager
{
    public Task<RecipeDetail> CreateAsync(int userId, RecipeData data) => throw new NotImplementedException();

    public Task<List<RecipeDetail>> ListAsync(int userId) => throw new NotImplementedException();

    public Task<RecipeDetail?> GetAsync(int id, int userId) => throw new NotImplementedException();

    public Task<RecipeDetail?> UpdateAsync(int id, int userId, RecipeData data) => throw new NotImplementedException();

    public Task<bool> DeleteAsync(int id, int userId) => throw new NotImplementedException();
}
