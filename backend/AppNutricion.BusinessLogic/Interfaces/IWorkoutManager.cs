using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;

namespace AppNutricion.BusinessLogic.Interfaces;

public record WorkoutSetDraft(int ExerciseId, int SetNumber, int Reps, decimal WeightKg);

public record WorkoutDraft(int? RoutineDayId, DateOnly Date, int DurationMinutes, string? Notes, List<WorkoutSetDraft>? Sets);

// Dato inválido → ArgumentException con el campo JSON (sets[2].reps). Sin peso registrado → ConflictException.
// No encontrado (o de otro usuario, o un routineDayId que no es visible) → null/false.
public interface IWorkoutManager
{
    // kcal estimadas = MET promedio de los ejercicios distintos × último peso × horas, redondeadas y congeladas (D22).
    Task<WorkoutLog?> CreateAsync(int userId, WorkoutDraft draft);

    // from y to obligatorios, from <= to, hasta 366 días.
    Task<List<WorkoutSummary>> ListAsync(int userId, DateOnly? from, DateOnly? to);

    Task<WorkoutLog?> GetAsync(int id, int userId);
    Task<bool> DeleteAsync(int id, int userId);
}
