using AppNutricion.BusinessLogic.Interfaces;

namespace AppNutricion.Presentation.Output;

public record DayTotalsOutput(DateOnly Date, decimal Calories, decimal ProteinG, decimal CarbsG, decimal FatG, bool Logged);

// TargetCalories: la línea de meta del gráfico (null sin objetivo activo).
public record WeeklySummaryOutput(List<DayTotalsOutput> Days, MacroTotals? Averages, int DaysLogged, decimal? TargetCalories, int Streak)
{
    public static WeeklySummaryOutput From(WeeklySummary s) => new(
        s.Days.Select(d => new DayTotalsOutput(d.Date, d.Totals.Calories, d.Totals.ProteinG, d.Totals.CarbsG, d.Totals.FatG, d.Logged)).ToList(),
        s.Averages, s.DaysLogged, s.TargetCalories, s.Streak);
}
