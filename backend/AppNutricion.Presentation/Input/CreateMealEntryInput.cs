using System.Text.Json.Serialization;
using AppNutricion.Entities.Enums;

namespace AppNutricion.Presentation.Input;

// Sin UserId a propósito: sale del token.
public record CreateMealEntryInput(
    int FoodId,
    decimal Quantity,
    bool IsUnits,
    [property: JsonConverter(typeof(JsonStringEnumConverter<MealType>))] MealType MealType,
    DateOnly Date,
    int? RecipeId);
