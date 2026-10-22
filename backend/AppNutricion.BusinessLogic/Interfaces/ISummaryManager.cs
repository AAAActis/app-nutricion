using AppNutricion.Entities.Enums;

namespace AppNutricion.BusinessLogic.Interfaces;

// Kcal enteras y macros con 1 decimal (progreso en %, con 1 decimal): se redondea solo al final.
public record MacroTotals(decimal Calories, decimal ProteinG, decimal CarbsG, decimal FatG);

// Target − consumido: negativo si se pasó (Exceeded).
public record MacroLeft(decimal Value, bool Exceeded);

public record RemainingMacros(MacroLeft Calories, MacroLeft ProteinG, MacroLeft CarbsG, MacroLeft FatG);

public record MealTypeTotals(MealType MealType, MacroTotals Totals, int Entries);

// Targets, Remaining y ProgressPct son null sin objetivo activo (o con uno que no se puede leer: sin perfil o sin peso).
public record DailySummary(
    DateOnly Date, MacroTotals Consumed, List<MealTypeTotals> ByMealType,
    MacroTotals? Targets, RemainingMacros? Remaining, MacroTotals? ProgressPct);

// Logged = el día tiene al menos una comida (sin comidas, todo en 0).
public record DayTotals(DateOnly Date, MacroTotals Totals, bool Logged);

// Averages: solo de los días con comidas (null si no hay ninguno). Streak: días seguidos con comidas hasta hoy
// del usuario (no hasta endDate); si hoy todavía no tiene, cuenta desde ayer.
public record WeeklySummary(List<DayTotals> Days, MacroTotals? Averages, int DaysLogged, decimal? TargetCalories, int Streak);

// Sin fecha = hoy del usuario. Fecha fuera del rango del diario → ArgumentException con el campo.
public interface ISummaryManager
{
    Task<DailySummary> GetDailyAsync(int userId, DateOnly? date);

    // Los 7 días que terminan en endDate (incluido), del más viejo al más nuevo.
    Task<WeeklySummary> GetWeeklyAsync(int userId, DateOnly? endDate);
}
