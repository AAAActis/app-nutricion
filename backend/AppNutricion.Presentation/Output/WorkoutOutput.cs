using AppNutricion.BusinessLogic.Constants;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;

namespace AppNutricion.Presentation.Output;

public record WorkoutSetOutput(int Id, int ExerciseId, string ExerciseName, int SetNumber, int Reps, decimal WeightKg);

// Las kcal son una estimación (MET × peso × horas): van siempre con el disclaimer.
public record WorkoutOutput(
    int Id, int? RoutineDayId, DateOnly Date, int DurationMinutes, decimal EstimatedCaloriesBurned, string? Notes,
    DateTime CreatedAt, List<WorkoutSetOutput> Sets, string Disclaimer)
{
    public static WorkoutOutput From(WorkoutLog w) => new(
        w.Id, w.RoutineDayId, w.Date, w.DurationMinutes, w.EstimatedCaloriesBurned, w.Notes, w.CreatedAt,
        w.Sets.Select(s => new WorkoutSetOutput(s.Id, s.ExerciseId, s.Exercise.Name, s.SetNumber, s.Reps, s.WeightKg)).ToList(),
        NutritionDefaults.Disclaimer);
}

public record WorkoutSummaryOutput(
    int Id, int? RoutineDayId, DateOnly Date, int DurationMinutes, decimal EstimatedCaloriesBurned, string? Notes,
    int SetCount, decimal VolumeKg, string Disclaimer)
{
    public static WorkoutSummaryOutput From(WorkoutSummary w) => new(
        w.Id, w.RoutineDayId, w.Date, w.DurationMinutes, w.EstimatedCaloriesBurned, w.Notes, w.SetCount, w.VolumeKg,
        NutritionDefaults.Disclaimer);
}
