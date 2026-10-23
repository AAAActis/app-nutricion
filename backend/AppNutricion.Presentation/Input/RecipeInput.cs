namespace AppNutricion.Presentation.Input;

// Sin UserId a propósito: sale del token.
public record RecipeInput(string? Name, int Servings, List<RecipeIngredientInput>? Ingredients);

public record RecipeIngredientInput(int FoodId, decimal QuantityGrams);
