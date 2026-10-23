using AppNutricion.Entities.Entities;

namespace AppNutricion.DataAccess.Interfaces;

// Todo filtra por userId: una receta de otro usuario no existe (null/false). Las lecturas traen los ingredientes con su Food.
public interface IRecipeStore
{
    Task<List<Recipe>> GetByUserAsync(int userId);  // por nombre
    Task<Recipe?> GetByIdAsync(int id, int userId);
    Task AddAsync(Recipe recipe);
    // Reemplaza nombre, porciones e ingredientes.
    Task<bool> UpdateAsync(Recipe recipe, int userId);
    Task<bool> DeleteAsync(int id, int userId);
}
