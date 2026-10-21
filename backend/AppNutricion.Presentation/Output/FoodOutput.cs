using AppNutricion.Entities.Entities;

namespace AppNutricion.Presentation.Output;

public record FoodOutput(
    int Id,
    string Name,
    string? Brand,
    string? Barcode,
    string Source,
    decimal CaloriesPer100g,
    decimal ProteinPer100g,
    decimal CarbsPer100g,
    decimal FatPer100g,
    string? PortionName,
    decimal? PortionWeightGrams,
    string? Attribution)
{
    public static FoodOutput From(Food f) => new(
        f.Id, f.Name, f.Brand, f.Barcode, f.Source.ToString(),
        f.CaloriesPer100g, f.ProteinPer100g, f.CarbsPer100g, f.FatPer100g,
        f.PortionName, f.PortionWeightGrams, null);
}
