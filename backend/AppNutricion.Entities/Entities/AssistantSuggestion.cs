using AppNutricion.Entities.Enums;

namespace AppNutricion.Entities.Entities;

// Propuesta del asistente de registrar una ingesta. El LLM solo elige FoodId y gramos:
// al aceptarla, la API crea el MealEntry con macros de la tabla Foods (decisiones D5 y D6).
public class AssistantSuggestion
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int ChatMessageId { get; set; }
    public int FoodId { get; set; }
    public Food Food { get; set; } = null!;
    public decimal QuantityGrams { get; set; }
    public MealType MealType { get; set; }
    public DateOnly Date { get; set; }
    public SuggestionStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public int? MealEntryId { get; set; }                   // el registro que generó, si se aceptó
}
