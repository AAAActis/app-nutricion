using AppNutricion.BusinessLogic.Calculators;
using AppNutricion.Entities.Entities;

namespace AppNutricion.BusinessLogic.Interfaces;

// Goal: kcal y macros congelados al crearlo. Live: el motor corrido con el perfil y el último peso
// (de ahí salen IMC, proyección y avisos).
public record GoalTargets(Goal Goal, NutritionResult Live);

public interface IGoalManager
{
    // Sin perfil o sin peso → ConflictException. Datos incoherentes → ArgumentException
    // (goalType / weeklyRateKg / targetWeightKg).
    Task<GoalTargets> CreateAsync(int userId, string? goalType, decimal? weeklyRateKg, decimal? targetWeightKg);

    Task<List<Goal>> ListAsync(int userId);

    // null = no hay objetivo activo.
    Task<GoalTargets?> GetTargetsAsync(int userId);
}
