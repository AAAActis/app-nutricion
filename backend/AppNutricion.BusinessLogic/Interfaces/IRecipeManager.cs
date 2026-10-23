using AppNutricion.Entities.Entities;

namespace AppNutricion.BusinessLogic.Interfaces;

public record RecipeIngredientData(int FoodId, decimal QuantityGrams);

public record RecipeData(string? Name, int Servings, List<RecipeIngredientData>? Ingredients);

public record IngredientNutrition(int FoodId, string FoodName, decimal QuantityGrams, MacroTotals Macros);

// Calculado desde Foods en cada lectura (no se guarda) y sin redondear: se redondea en el Output.
public record RecipeDetail(Recipe Recipe, List<IngredientNutrition> Ingredients, MacroTotals Totals, MacroTotals PerServing, decimal TotalGrams);

// Dato inválido → ArgumentException con el campo. Receta de otro usuario → null/false.
public interface IRecipeManager
{
    Task<RecipeDetail> CreateAsync(int userId, RecipeData data);
    Task<List<RecipeDetail>> ListAsync(int userId);
    Task<RecipeDetail?> GetAsync(int id, int userId);
    // Reemplaza nombre, porciones e ingredientes; las comidas ya cargadas no cambian.
    Task<RecipeDetail?> UpdateAsync(int id, int userId, RecipeData data);
    Task<bool> DeleteAsync(int id, int userId);
}
