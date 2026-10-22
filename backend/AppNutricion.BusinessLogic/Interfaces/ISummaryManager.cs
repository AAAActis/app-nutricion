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

// Sin fecha = hoy del usuario. Fecha fuera del rango del diario → ArgumentException con el campo.
public interface ISummaryManager
{
    Task<DailySummary> GetDailyAsync(int userId, DateOnly? date);
}
