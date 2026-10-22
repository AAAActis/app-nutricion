using AppNutricion.Entities.Entities;

namespace AppNutricion.DataAccess.Interfaces;

// Un entrenamiento con sus totales sumados en la base. Volumen = Σ reps × kg.
public record WorkoutSummary(
    int Id, int? RoutineDayId, DateOnly Date, int DurationMinutes, decimal EstimatedCaloriesBurned, string? Notes,
    int SetCount, decimal VolumeKg);

// Todo filtra por userId: un entrenamiento de otro usuario no existe (null/false).
public interface IWorkoutStore
{
    Task AddAsync(WorkoutLog workout);

    // Con sus sets (en el orden en que se cargaron) y el Exercise de cada uno.
    Task<WorkoutLog?> GetAsync(int id, int userId);

    // Entre from y to (incluidos), del más nuevo al más viejo.
    Task<List<WorkoutSummary>> ListAsync(int userId, DateOnly from, DateOnly to);

    Task<bool> DeleteAsync(int id, int userId);
}
