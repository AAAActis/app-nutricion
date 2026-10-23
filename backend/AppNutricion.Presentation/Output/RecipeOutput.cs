using AppNutricion.BusinessLogic.Interfaces;

namespace AppNutricion.Presentation.Output;

public record RecipeIngredientOutput(int FoodId, string FoodName, decimal QuantityGrams, decimal Calories, decimal ProteinG, decimal CarbsG, decimal FatG);

// Calculado desde Foods en cada pedido; acá (y solo acá) se redondea a 1 decimal.
public record RecipeOutput(
    int Id,
    string Name,
    int Servings,
    decimal TotalGrams,
    MacroTotals Totals,
    MacroTotals PerServing,
    List<RecipeIngredientOutput> Ingredients,
    DateTime CreatedAt)
{
    public static RecipeOutput From(RecipeDetail d) => new(
        d.Recipe.Id, d.Recipe.Name, d.Recipe.Servings, Round(d.TotalGrams), Round(d.Totals), Round(d.PerServing),
        d.Ingredients.Select(i => new RecipeIngredientOutput(i.FoodId, i.FoodName, Round(i.QuantityGrams),
            Round(i.Macros.Calories), Round(i.Macros.ProteinG), Round(i.Macros.CarbsG), Round(i.Macros.FatG))).ToList(),
        d.Recipe.CreatedAt);

    private static decimal Round(decimal value) => Math.Round(value, 1);

    private static MacroTotals Round(MacroTotals m) => new(Round(m.Calories), Round(m.ProteinG), Round(m.CarbsG), Round(m.FatG));
}
