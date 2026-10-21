using AppNutricion.Entities.Enums;

namespace AppNutricion.Entities.Entities;

public class Food
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string NormalizedName { get; set; } = null!;     // minúsculas y sin tildes, para la búsqueda trigram
    public string? Brand { get; set; }
    public string? Barcode { get; set; }
    public FoodSource Source { get; set; }
    public string? ExternalId { get; set; }
    public int? CreatedByUserId { get; set; }                // solo los Custom tienen dueño
    public decimal CaloriesPer100g { get; set; }
    public decimal ProteinPer100g { get; set; }
    public decimal CarbsPer100g { get; set; }
    public decimal FatPer100g { get; set; }
    public string? PortionName { get; set; }
    public decimal? PortionWeightGrams { get; set; }
    public DateTime CreatedAt { get; set; }
}
