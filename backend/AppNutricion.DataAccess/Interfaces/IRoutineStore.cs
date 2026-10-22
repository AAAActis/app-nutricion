using AppNutricion.Entities.Entities;

namespace AppNutricion.DataAccess.Interfaces;

// "Visible" = precargada (UserId null) o del usuario. Una rutina de otro usuario no existe (null/false).
public interface IRoutineStore
{
    // Precargadas primero y después las propias, cada grupo por nombre. Sin días.
    Task<List<Routine>> ListVisibleAsync(int userId);

    // Con días (por DayNumber) y ejercicios (por Order) con su Exercise.
    Task<Routine?> GetVisibleAsync(int id, int userId);

    Task<bool> IsDayVisibleAsync(int routineDayId, int userId);

    Task AddAsync(Routine routine);

    // Solo las del usuario: una precargada o de otro usuario no se borra (false).
    // Los WorkoutLogs que usaban sus días quedan con RoutineDayId null (FK SetNull).
    Task<bool> DeleteOwnAsync(int id, int userId);

    // Seed: inserta las precargadas que no existen por Name (con UserId null). Devuelve cuántas insertó.
    Task<int> AddMissingPresetsAsync(IEnumerable<Routine> routines);
}
