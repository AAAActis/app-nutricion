namespace AppNutricion.Entities.Entities;

// Una serie realizada. Volumen = Reps × WeightKg (se calcula, no se guarda).
public class WorkoutSet
{
    public int Id { get; set; }
    public int WorkoutLogId { get; set; }
    public int ExerciseId { get; set; }
    public Exercise Exercise { get; set; } = null!;
    public int SetNumber { get; set; }
    public int Reps { get; set; }
    public decimal WeightKg { get; set; }
}
