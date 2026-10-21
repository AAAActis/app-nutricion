namespace AppNutricion.Entities.Entities;

// Un entrenamiento realizado (una sesión).
public class WorkoutLog
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int? RoutineDayId { get; set; }                  // null = entrenamiento libre
    public DateOnly Date { get; set; }
    public int DurationMinutes { get; set; }
    public decimal EstimatedCaloriesBurned { get; set; }    // estimación congelada al guardar
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<WorkoutSet> Sets { get; set; } = new List<WorkoutSet>();
}
