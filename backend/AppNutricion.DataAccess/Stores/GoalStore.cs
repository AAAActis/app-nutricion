using AppNutricion.DataAccess.DataBaseContext;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using Microsoft.EntityFrameworkCore;

namespace AppNutricion.DataAccess.Stores;

public class GoalStore(ApplicationDbContext context) : IGoalStore
{
    public Task<Goal?> GetActiveAsync(int userId) =>
        context.Goals.AsNoTracking().FirstOrDefaultAsync(g => g.UserId == userId && g.IsActive);

    public Task<List<Goal>> ListAsync(int userId) =>
        context.Goals.AsNoTracking().Where(g => g.UserId == userId)
            .OrderByDescending(g => g.CreatedAt).ThenByDescending(g => g.Id).ToListAsync();

    public async Task AddAsActiveAsync(Goal goal)
    {
        // En una transacción: ExecuteUpdate manda el UPDATE que desactiva a la base antes del INSERT,
        // porque el índice único parcial ("IsActive" = true) se chequea en cada sentencia.
        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.Goals.Where(g => g.UserId == goal.UserId && g.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.IsActive, false));
        goal.IsActive = true;
        context.Goals.Add(goal);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
