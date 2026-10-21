using AppNutricion.Entities.Enums;

namespace AppNutricion.Entities.Entities;

// Las macros se congelan al crearse con los valores de Foods: no se recalculan después.
public class MealEntry
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int? FoodId { get; set; }
    public Food? Food { get; set; }
    public int? RecipeId { get; set; }
    public Recipe? Recipe { get; set; }
    public string ItemName { get; set; } = null!;           // nombre copiado: sobrevive al borrado del alimento
    public DateOnly Date { get; set; }                      // día en la zona horaria del perfil
    public MealType MealType { get; set; }
    public decimal QuantityGrams { get; set; }
    public decimal SavedCalories { get; set; }
    public decimal SavedProtein { get; set; }
    public decimal SavedCarbs { get; set; }
    public decimal SavedFat { get; set; }
    public DateTime CreatedAt { get; set; }
}
