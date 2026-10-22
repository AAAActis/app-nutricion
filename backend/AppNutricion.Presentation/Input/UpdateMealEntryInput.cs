using System.Text.Json.Serialization;
using AppNutricion.Entities.Enums;

namespace AppNutricion.Presentation.Input;

// Todo opcional: lo que no viene no cambia.
public record UpdateMealEntryInput(
    decimal? Quantity,
    bool? IsUnits,
    [property: JsonConverter(typeof(JsonStringEnumConverter<MealType>))] MealType? MealType,
    DateOnly? Date);
