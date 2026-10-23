using AppNutricion.Entities.Entities;

namespace AppNutricion.Presentation.Output;

public record ExerciseOutput(int Id, string Name, string MuscleGroup, decimal MetValue)
{
    public static ExerciseOutput From(Exercise e) => new(e.Id, e.Name, e.MuscleGroup.ToString(), e.MetValue);
}

public record RoutineSummaryOutput(int Id, string Name, string? Description, bool IsPreset)
{
    public static RoutineSummaryOutput From(Routine r) => new(r.Id, r.Name, r.Description, r.UserId is null);
}

public record RoutineExerciseOutput(
    int ExerciseId, string ExerciseName, string MuscleGroup, int Order, int TargetSets, int TargetReps, decimal? TargetWeightKg);

public record RoutineDayOutput(int Id, int DayNumber, string Name, List<RoutineExerciseOutput> Exercises);

public record RoutineOutput(int Id, string Name, string? Description, bool IsPreset, List<RoutineDayOutput> Days)
{
    public static RoutineOutput From(Routine r) => new(r.Id, r.Name, r.Description, r.UserId is null,
        r.Days.Select(d => new RoutineDayOutput(d.Id, d.DayNumber, d.Name,
            d.Exercises.Select(e => new RoutineExerciseOutput(
                e.ExerciseId, e.Exercise.Name, e.Exercise.MuscleGroup.ToString(), e.Order, e.TargetSets, e.TargetReps, e.TargetWeightKg)).ToList()))
        .ToList());
}
