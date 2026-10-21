namespace AppNutricion.Entities.Entities;

public class RoutineDay
{
    public int Id { get; set; }
    public int RoutineId { get; set; }
    public int DayNumber { get; set; }                      // orden dentro de la rutina (1, 2, 3…)
    public string Name { get; set; } = null!;               // "Empuje", "Piernas"

    public ICollection<RoutineExercise> Exercises { get; set; } = new List<RoutineExercise>();
}
