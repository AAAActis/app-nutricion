namespace AppNutricion.Entities.Entities;

// Los totales y el "por porción" se calculan en el Manager, no se guardan.
public class Recipe
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = null!;
    public int Servings { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<RecipeIngredient> Ingredients { get; set; } = new List<RecipeIngredient>();
}
