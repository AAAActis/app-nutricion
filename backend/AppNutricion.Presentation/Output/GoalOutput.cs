using AppNutricion.BusinessLogic.Constants;
using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.Entities.Entities;

namespace AppNutricion.Presentation.Output;

public record GoalOutput(
    int Id,
    string GoalType,
    decimal? TargetWeightKg,
    decimal WeeklyRateKg,
    decimal TargetCalories,
    decimal TargetProteinG,
    decimal TargetCarbsG,
    decimal TargetFatG,
    bool IsActive,
    DateTime CreatedAt)
{
    public static GoalOutput From(Goal g) => new(
        g.Id, g.GoalType.ToString(), g.TargetWeightKg, g.WeeklyRateKg,
        g.TargetCalories, g.TargetProteinG, g.TargetCarbsG, g.TargetFatG, g.IsActive, g.CreatedAt);
}

public record ProjectionOutput(decimal Weeks, DateOnly EstimatedDate);

public record WarningOutput(string Code, string Message);

// Kcal y macros: congelados en el objetivo. IMC, proyección y avisos: con el último peso.
public record TargetsOutput(
    string GoalType,
    decimal WeeklyRateKg,
    decimal AppliedWeeklyRateKg,
    decimal? TargetWeightKg,
    decimal Calories,
    decimal ProteinG,
    decimal CarbsG,
    decimal FatG,
    decimal Bmi,
    string BmiCategory,
    ProjectionOutput? Projection,
    List<WarningOutput> Warnings,
    string Disclaimer)
{
    public static TargetsOutput From(GoalTargets t)
    {
        var (goal, live) = (t.Goal, t.Live);
        var warnings = new List<WarningOutput>();
        if (live.WeeklyRateCapped)
            warnings.Add(new WarningOutput("WeeklyRateCapped",
                $"El ritmo pedido supera el máximo seguro: se calculó con {live.AppliedWeeklyRateKg} kg por semana."));
        if (live.CalorieFloorApplied)
            warnings.Add(new WarningOutput("CalorieFloorApplied",
                "El cálculo quedaba por debajo del mínimo diario recomendado: se usa el mínimo."));

        return new TargetsOutput(
            goal.GoalType.ToString(), goal.WeeklyRateKg, live.AppliedWeeklyRateKg, goal.TargetWeightKg,
            goal.TargetCalories, goal.TargetProteinG, goal.TargetCarbsG, goal.TargetFatG,
            live.Bmi, live.BmiCategory.ToString(),
            live.ProjectedWeeks is { } weeks && live.ProjectedDate is { } date ? new ProjectionOutput(weeks, date) : null,
            warnings, NutritionDefaults.Disclaimer);
    }
}
