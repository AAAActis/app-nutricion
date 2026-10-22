using AppNutricion.Entities.Entities;

namespace AppNutricion.Presentation.Output;

public record MealEntryOutput(
    int Id,
    int? FoodId,
    int? RecipeId,
    string ItemName,
    DateOnly Date,
    string MealType,
    decimal QuantityGrams,
    decimal SavedCalories,
    decimal SavedProtein,
    decimal SavedCarbs,
    decimal SavedFat,
    DateTime CreatedAt)
{
    public static MealEntryOutput From(MealEntry m) => new(
        m.Id, m.FoodId, m.RecipeId, m.ItemName, m.Date, m.MealType.ToString(), m.QuantityGrams,
        m.SavedCalories, m.SavedProtein, m.SavedCarbs, m.SavedFat, m.CreatedAt);
}
