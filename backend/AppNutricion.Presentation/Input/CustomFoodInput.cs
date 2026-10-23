namespace AppNutricion.Presentation.Input;

// Sin dueño, fuente ni código de barras a propósito: el dueño sale del token y la fuente es siempre Custom.
public record CustomFoodInput(
    string? Name,
    string? Brand,
    decimal? CaloriesPer100g,
    decimal? ProteinPer100g,
    decimal? CarbsPer100g,
    decimal? FatPer100g,
    string? PortionName,
    decimal? PortionWeightGrams);
