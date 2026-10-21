namespace AppNutricion.Entities.Entities;

public class Routine
{
    public int Id { get; set; }
    public int? UserId { get; set; }                        // null = rutina precargada (compartida)
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<RoutineDay> Days { get; set; } = new List<RoutineDay>();
}
