namespace AppNutricion.Entities.Entities;

// Lo planificado. Lo realizado va en WorkoutSet.
public class RoutineExercise
{
    public int Id { get; set; }
    public int RoutineDayId { get; set; }
    public int ExerciseId { get; set; }
    public Exercise Exercise { get; set; } = null!;
    public int Order { get; set; }
    public int TargetSets { get; set; }
    public int TargetReps { get; set; }
    public decimal? TargetWeightKg { get; set; }
}
