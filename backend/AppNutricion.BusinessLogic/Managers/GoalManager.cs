using AppNutricion.BusinessLogic.Calculators;
using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.Entities.Entities;

namespace AppNutricion.BusinessLogic.Managers;

public class GoalManager : IGoalManager
{
    public Task<GoalTargets> CreateAsync(int userId, string? goalType, decimal? weeklyRateKg, decimal? targetWeightKg) =>
        Task.FromResult(new GoalTargets(new Goal(), new NutritionResult(0, 0, 0, 0, 0, default, 0, false, false, null, null)));

    public Task<List<Goal>> ListAsync(int userId) => Task.FromResult(new List<Goal>());

    public Task<GoalTargets?> GetTargetsAsync(int userId) => Task.FromResult<GoalTargets?>(null);
}
