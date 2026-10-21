namespace AppNutricion.Entities.Entities;

public class RecipeIngredient
{
    public int Id { get; set; }
    public int RecipeId { get; set; }
    public int FoodId { get; set; }
    public Food Food { get; set; } = null!;
    public decimal QuantityGrams { get; set; }
}
