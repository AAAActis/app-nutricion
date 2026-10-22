using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;

namespace AppNutricion.Tests.Infraestructura;

// Imita a Goals con su índice único parcial: si al insertar un activo ya hay otro activo, falla
// (como Postgres). Por eso AddAsActiveAsync desactiva primero.
public sealed class GoalStoreEnMemoria : IGoalStore
{
    private readonly List<Goal> _goals = [];
    private readonly Lock _lock = new();
    private int _ultimoId;

    public Task<Goal?> GetActiveAsync(int userId)
    {
        lock (_lock)
            return Task.FromResult(_goals.Where(g => g.UserId == userId && g.IsActive).Select(Copia).FirstOrDefault());
    }

    public Task<List<Goal>> ListAsync(int userId)
    {
        lock (_lock)
            return Task.FromResult(_goals.Where(g => g.UserId == userId)
                .OrderByDescending(g => g.CreatedAt).ThenByDescending(g => g.Id).Select(Copia).ToList());
    }

    public Task AddAsActiveAsync(Goal goal)
    {
        lock (_lock)
        {
            foreach (var activo in _goals.Where(g => g.UserId == goal.UserId && g.IsActive))
                activo.IsActive = false;
            if (_goals.Any(g => g.UserId == goal.UserId && g.IsActive))
                throw new InvalidOperationException("Violación del índice único parcial: ya hay un objetivo activo.");
            goal.Id = ++_ultimoId;
            goal.IsActive = true;
            _goals.Add(Copia(goal));
            return Task.CompletedTask;
        }
    }

    private static Goal Copia(Goal g) => new()
    {
        Id = g.Id, UserId = g.UserId, GoalType = g.GoalType, TargetWeightKg = g.TargetWeightKg,
        WeeklyRateKg = g.WeeklyRateKg, TargetCalories = g.TargetCalories, TargetProteinG = g.TargetProteinG,
        TargetCarbsG = g.TargetCarbsG, TargetFatG = g.TargetFatG, IsActive = g.IsActive, CreatedAt = g.CreatedAt,
    };
}
