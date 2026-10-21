using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;

namespace AppNutricion.BusinessLogic.Validators;

// Reglas para alimentos que vienen de afuera (seed, Open Food Facts). Devuelve el motivo o null si es válido.
public static class FoodValidator
{
    private const decimal MaxCaloriesPer100g = 900m; // grasa pura ≈ 900 kcal
    private const decimal MaxMacrosPer100g = 100m;

    public static string? GetInvalidReason(Food food)
    {
        if (string.IsNullOrWhiteSpace(food.Name)) return "falta name";
        if (string.IsNullOrWhiteSpace(food.ExternalId)) return "falta externalId";
        if (food.Source == FoodSource.Custom) return "source Custom no se acepta de afuera";
        if (food.CaloriesPer100g < 0 || food.ProteinPer100g < 0 || food.CarbsPer100g < 0 || food.FatPer100g < 0)
            return "macro negativo";
        if (food.CaloriesPer100g > MaxCaloriesPer100g) return $"más de {MaxCaloriesPer100g} kcal por 100 g";
        if (food.ProteinPer100g + food.CarbsPer100g + food.FatPer100g > MaxMacrosPer100g)
            return $"proteína + carbos + grasas suman más de {MaxMacrosPer100g} g por 100 g";
        // Largos de FoodConfiguration: uno que no entra haría fallar el SaveChanges de todo el lote.
        if (food.Name.Length > 200 || food.ExternalId.Length > 64 || food.Brand?.Length > 120
            || food.Barcode?.Length > 32 || food.PortionName?.Length > 60)
            return "algún texto supera el largo de su columna";
        return null;
    }
}
