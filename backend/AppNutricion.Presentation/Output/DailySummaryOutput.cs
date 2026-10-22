using AppNutricion.BusinessLogic.Constants;
using AppNutricion.BusinessLogic.Interfaces;

namespace AppNutricion.Presentation.Output;

public record MealTypeTotalsOutput(string MealType, decimal Calories, decimal ProteinG, decimal CarbsG, decimal FatG, int Entries);

// Targets, Remaining y ProgressPct en null: no hay objetivo activo.
public record DailySummaryOutput(
    DateOnly Date,
    MacroTotals Consumed,
    List<MealTypeTotalsOutput> ByMealType,
    MacroTotals? Targets,
    RemainingMacros? Remaining,
    MacroTotals? ProgressPct,
    string Disclaimer)
{
    public static DailySummaryOutput From(DailySummary s) => new(
        s.Date, s.Consumed,
        s.ByMealType.Select(t => new MealTypeTotalsOutput(
            t.MealType.ToString(), t.Totals.Calories, t.Totals.ProteinG, t.Totals.CarbsG, t.Totals.FatG, t.Entries)).ToList(),
        s.Targets, s.Remaining, s.ProgressPct, NutritionDefaults.Disclaimer);
}
