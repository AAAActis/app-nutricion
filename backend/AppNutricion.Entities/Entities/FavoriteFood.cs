namespace AppNutricion.Entities.Entities;

// "Recientes" no tiene tabla: sale de MealEntries.
public class FavoriteFood
{
    public int UserId { get; set; }
    public int FoodId { get; set; }
    public Food Food { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}
