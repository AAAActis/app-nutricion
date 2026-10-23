using System.Text.Json.Serialization;
using AppNutricion.Entities.Enums;

namespace AppNutricion.Presentation.Input;

// Porciones de la receta (admite 0.5), no gramos. Sin UserId: sale del token.
public record CreateRecipeMealInput(
    int RecipeId,
    decimal Servings,
    [property: JsonConverter(typeof(JsonStringEnumConverter<MealType>))] MealType MealType,
    DateOnly Date);
