using System.Text.Json.Serialization;
using AppNutricion.Entities.Enums;

namespace AppNutricion.Presentation.Input;

// Sin MealType copia el día entero.
public record CopyMealsInput(
    DateOnly FromDate,
    DateOnly ToDate,
    [property: JsonConverter(typeof(JsonStringEnumConverter<MealType>))] MealType? MealType);
