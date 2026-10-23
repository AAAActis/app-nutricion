using AppNutricion.Entities.Entities;

namespace AppNutricion.BusinessLogic.Interfaces;

public record RoutineExerciseDraft(int ExerciseId, int Order, int TargetSets, int TargetReps, decimal? TargetWeightKg);

public record RoutineDayDraft(int DayNumber, string? Name, List<RoutineExerciseDraft>? Exercises);

public record RoutineDraft(string? Name, string? Description, List<RoutineDayDraft>? Days);

// Dato inválido → ArgumentException con el campo JSON (days[0].exercises[1].exerciseId).
// No encontrado (o de otro usuario) → null/false. Precargada al borrar → ConflictException.
public interface IRoutineManager
{
    // muscleGroup: nombre del enum MuscleGroup, sin distinguir mayúsculas; null = todos.
    Task<List<Exercise>> ListExercisesAsync(string? muscleGroup);

    Task<List<Routine>> ListAsync(int userId);
    Task<Routine?> GetAsync(int id, int userId);

    // Devuelve la rutina guardada con días y ejercicios.
    Task<Routine> CreateAsync(int userId, RoutineDraft draft);

    Task<bool> DeleteAsync(int id, int userId);
}
