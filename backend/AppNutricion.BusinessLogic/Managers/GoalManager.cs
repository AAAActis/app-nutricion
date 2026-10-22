using AppNutricion.BusinessLogic.Calculators;
using AppNutricion.BusinessLogic.Constants;
using AppNutricion.BusinessLogic.Exceptions;
using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.BusinessLogic.Validators;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;

namespace AppNutricion.BusinessLogic.Managers;

public class GoalManager(IGoalStore goalStore, IProfileStore profileStore, IBodyMetricStore metricStore, TimeProvider timeProvider) : IGoalManager
{
    public const string MissingWeightMessage = "Primero registrá tu peso.";

    public async Task<GoalTargets> CreateAsync(int userId, string? goalType, decimal? weeklyRateKg, decimal? targetWeightKg)
    {
        var (profile, today) = await ProfileGuard.RequireAsync(profileStore, userId, timeProvider);
        var weight = await LatestWeightAsync(userId) ?? throw new ConflictException(MissingWeightMessage);

        if (!EnumInput.TryParse<GoalType>(goalType, out var type))
            throw new ArgumentException($"El objetivo tiene que ser uno de: {string.Join(", ", Enum.GetNames<GoalType>())}.", "goalType");
        var rate = ValidateRate(type, weeklyRateKg);
        ValidateTarget(type, targetWeightKg, weight);

        // Menores de 18 con LoseFat: el motor lanza ArgumentException("goalType").
        var result = NutritionCalculator.Calculate(InputFor(profile, weight, type, rate, targetWeightKg, today));

        // Se congelan kcal y macros, y WeeklyRateKg guarda el ritmo aplicado (con el que salieron esas kcal).
        // Si el pedido se recortó, el aviso sale solo en esta respuesta.
        var goal = new Goal
        {
            UserId = userId,
            GoalType = type,
            TargetWeightKg = targetWeightKg,
            WeeklyRateKg = result.AppliedWeeklyRateKg,
            TargetCalories = result.Calories,
            TargetProteinG = result.ProteinG,
            TargetCarbsG = result.CarbsG,
            TargetFatG = result.FatG,
            IsActive = true,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
        };
        await goalStore.AddAsActiveAsync(goal);
        return new GoalTargets(goal, result);
    }

    public Task<List<Goal>> ListAsync(int userId) => goalStore.ListAsync(userId);

    public async Task<GoalTargets?> GetTargetsAsync(int userId)
    {
        var goal = await goalStore.GetActiveAsync(userId);
        if (goal is null) return null;

        // Kcal y macros vienen congelados en el Goal; IMC, proyección y el aviso de piso se recalculan con el
        // último peso. El ritmo guardado ya es el aplicado: no se vuelve a recortar.
        var (profile, today) = await ProfileGuard.RequireAsync(profileStore, userId, timeProvider);
        var weight = await LatestWeightAsync(userId) ?? throw new ConflictException(MissingWeightMessage);
        var live = NutritionCalculator.Calculate(
            InputFor(profile, weight, goal.GoalType, goal.WeeklyRateKg, goal.TargetWeightKg, today) with { ApplyRateLimits = false });
        return new GoalTargets(goal, live);
    }

    private async Task<decimal?> LatestWeightAsync(int userId) =>
        (await metricStore.GetLatestAsync(userId, 1)).FirstOrDefault()?.WeightKg;

    // Maintain no lleva ritmo (null o 0). LoseFat y GainMuscle necesitan un ritmo positivo; si supera el
    // máximo seguro, el motor lo recorta y avisa.
    private static decimal ValidateRate(GoalType type, decimal? weeklyRateKg)
    {
        if (type == GoalType.Maintain)
        {
            if (weeklyRateKg is null or 0m) return 0m;
            throw new ArgumentException("Mantener no lleva ritmo semanal.", "weeklyRateKg");
        }
        if (weeklyRateKg is not { } rate || rate <= 0m)
            throw new ArgumentException("El ritmo semanal tiene que ser mayor a 0 kg.", "weeklyRateKg");
        return rate;
    }

    private static void ValidateTarget(GoalType type, decimal? targetWeightKg, decimal currentWeightKg)
    {
        if (targetWeightKg is not { } target) return;
        if (target < HealthDataLimits.MinWeightKg || target > HealthDataLimits.MaxWeightKg)
            throw new ArgumentException($"El peso meta tiene que estar entre {HealthDataLimits.MinWeightKg} y {HealthDataLimits.MaxWeightKg} kg.", "targetWeightKg");
        var coherent = type switch
        {
            GoalType.LoseFat => target < currentWeightKg,
            GoalType.GainMuscle => target > currentWeightKg,
            _ => false,   // Maintain no lleva meta
        };
        if (!coherent)
            throw new ArgumentException("El peso meta no es coherente con el objetivo y el peso actual.", "targetWeightKg");
    }

    private static NutritionInput InputFor(Profile profile, decimal weightKg, GoalType type, decimal rate, decimal? target, DateOnly today) =>
        new(profile.BiologicalSex, profile.BirthDate, profile.HeightCm, weightKg, profile.ActivityLevel, type, rate, target, today);
}
