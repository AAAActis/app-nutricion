using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;

namespace AppNutricion.BusinessLogic.Managers;

public class RecipeManager(IRecipeStore store, IFoodManager foodManager, TimeProvider timeProvider) : IRecipeManager
{
    private const int MaxNameLength = 120;
    private const int MaxServings = 50;
    private const int MaxIngredients = 30;
    private const decimal MaxGrams = 5000;

    public async Task<RecipeDetail> CreateAsync(int userId, RecipeData data)
    {
        var (recipe, foods) = await ValidAsync(userId, data);
        recipe.UserId = userId;
        recipe.CreatedAt = timeProvider.GetUtcNow().UtcDateTime;
        await store.AddAsync(recipe);
        return Detail(recipe, foods);
    }

    public async Task<List<RecipeDetail>> ListAsync(int userId) =>
        (await store.GetByUserAsync(userId)).Select(Detail).ToList();

    public async Task<RecipeDetail?> GetAsync(int id, int userId) =>
        await store.GetByIdAsync(id, userId) is { } recipe ? Detail(recipe) : null;

    public async Task<RecipeDetail?> UpdateAsync(int id, int userId, RecipeData data)
    {
        var current = await store.GetByIdAsync(id, userId);
        if (current is null) return null;
        var (recipe, foods) = await ValidAsync(userId, data);
        (recipe.Id, recipe.UserId, recipe.CreatedAt) = (current.Id, userId, current.CreatedAt);
        return await store.UpdateAsync(recipe, userId) ? Detail(recipe, foods) : null;
    }

    public Task<bool> DeleteAsync(int id, int userId) => store.DeleteAsync(id, userId);

    // Cada alimento tiene que ser visible para el usuario (compartido o Custom suyo): el Custom de otro en una receta
    // haría fallar el DELETE /api/me de su dueño (RecipeIngredients.FoodId es NoAction).
    // Los ingredientes salen sin el Food cargado: si no, EF intentaría insertar el alimento leído sin tracking.
    private async Task<(Recipe Recipe, Dictionary<int, Food> Foods)> ValidAsync(int userId, RecipeData data)
    {
        var name = data.Name?.Trim();
        if (name is not { Length: >= 1 and <= MaxNameLength })
            throw new ArgumentException($"El nombre tiene que tener entre 1 y {MaxNameLength} caracteres.", "name");
        if (data.Servings is < 1 or > MaxServings)
            throw new ArgumentException($"Las porciones van de 1 a {MaxServings}.", "servings");
        if (data.Ingredients is not { Count: >= 1 and <= MaxIngredients } ingredients)
            throw new ArgumentException($"La receta lleva de 1 a {MaxIngredients} ingredientes.", "ingredients");

        var foods = new Dictionary<int, Food>();
        var recipe = new Recipe { Name = name, Servings = data.Servings };
        for (var i = 0; i < ingredients.Count; i++)
        {
            var grams = Math.Round(ingredients[i].QuantityGrams, 2);
            if (grams is <= 0 or > MaxGrams)
                throw new ArgumentException($"La cantidad tiene que ser mayor a 0 y de hasta {MaxGrams} g.", $"ingredients[{i}].quantityGrams");
            var foodId = ingredients[i].FoodId;
            if (foods.ContainsKey(foodId))
                throw new ArgumentException("Ese alimento ya está en la receta: sumá los gramos en una sola línea.", $"ingredients[{i}].foodId");
            foods[foodId] = await foodManager.GetByIdAsync(foodId, userId)
                ?? throw new ArgumentException("No existe ese alimento.", $"ingredients[{i}].foodId");
            recipe.Ingredients.Add(new RecipeIngredient { FoodId = foodId, QuantityGrams = grams });
        }
        return (recipe, foods);
    }

    private static RecipeDetail Detail(Recipe recipe) => Detail(recipe, recipe.Ingredients.ToDictionary(i => i.FoodId, i => i.Food));

    // Desde Foods y sin redondear (D6): el Output redondea.
    private static RecipeDetail Detail(Recipe recipe, Dictionary<int, Food> foods)
    {
        var ingredients = recipe.Ingredients.Select(i =>
        {
            var food = foods[i.FoodId];
            var g = i.QuantityGrams / 100;
            return new IngredientNutrition(food.Id, food.Name, i.QuantityGrams,
                new MacroTotals(g * food.CaloriesPer100g, g * food.ProteinPer100g, g * food.CarbsPer100g, g * food.FatPer100g));
        }).ToList();
        var totals = new MacroTotals(
            ingredients.Sum(i => i.Macros.Calories), ingredients.Sum(i => i.Macros.ProteinG),
            ingredients.Sum(i => i.Macros.CarbsG), ingredients.Sum(i => i.Macros.FatG));
        var perServing = new MacroTotals(
            totals.Calories / recipe.Servings, totals.ProteinG / recipe.Servings, totals.CarbsG / recipe.Servings, totals.FatG / recipe.Servings);
        return new RecipeDetail(recipe, ingredients, totals, perServing, recipe.Ingredients.Sum(i => i.QuantityGrams));
    }
}
