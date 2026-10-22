using AppNutricion.Entities.Entities;

namespace AppNutricion.DataAccess.Interfaces;

public interface IGoalStore
{
    Task<Goal?> GetActiveAsync(int userId);

    // Historial, del más nuevo al más viejo.
    Task<List<Goal>> ListAsync(int userId);

    // En una transacción: primero desactiva el objetivo activo (si hay) y después inserta este como activo.
    // El orden importa: el índice único parcial de Goals permite un solo activo por usuario.
    Task AddAsActiveAsync(Goal goal);
}
