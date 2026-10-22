using AppNutricion.BusinessLogic.Exceptions;
using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;

namespace AppNutricion.BusinessLogic.Managers;

public class SummaryManager(IMealEntryStore store, IGoalManager goalManager, IUserToday userToday) : ISummaryManager
{
    private const int MaxStreakDays = 366;

    public async Task<DailySummary> GetDailyAsync(int userId, DateOnly? date)
    {
        var day = await DayAsync(userId, date, nameof(date));
        var byType = (await store.SumByMealTypeAsync(userId, day)).OrderBy(p => p.Key).ToList();
        var consumed = new MacroTotals(
            byType.Sum(p => p.Value.Calories), byType.Sum(p => p.Value.Protein), byType.Sum(p => p.Value.Carbs), byType.Sum(p => p.Value.Fat));
        var byMealType = byType.Select(p => new MealTypeTotals(p.Key, Round(ToMacros(p.Value)), p.Value.Entries)).ToList();

        var goal = await ActiveGoalAsync(userId);
        if (goal is null) return new DailySummary(day, Round(consumed), byMealType, null, null, null);

        var targets = new MacroTotals(goal.TargetCalories, goal.TargetProteinG, goal.TargetCarbsG, goal.TargetFatG);
        var remaining = new RemainingMacros(
            Left(targets.Calories, consumed.Calories, 0), Left(targets.ProteinG, consumed.ProteinG, 1),
            Left(targets.CarbsG, consumed.CarbsG, 1), Left(targets.FatG, consumed.FatG, 1));
        var progress = new MacroTotals(
            Pct(consumed.Calories, targets.Calories), Pct(consumed.ProteinG, targets.ProteinG),
            Pct(consumed.CarbsG, targets.CarbsG), Pct(consumed.FatG, targets.FatG));
        return new DailySummary(day, Round(consumed), byMealType, Round(targets), remaining, progress);
    }

    public async Task<WeeklySummary> GetWeeklyAsync(int userId, DateOnly? endDate)
    {
        var end = await DayAsync(userId, endDate, nameof(endDate));
        var start = end.AddDays(-6);
        var sums = await store.SumByDateAsync(userId, start, end);
        var days = Enumerable.Range(0, 7).Select(i => start.AddDays(i)).Select(d => sums.TryGetValue(d, out var t)
            ? new DayTotals(d, Round(ToMacros(t)), true)
            : new DayTotals(d, new MacroTotals(0, 0, 0, 0), false)).ToList();

        var logged = sums.Values.Select(ToMacros).ToList();
        var averages = logged.Count == 0 ? null : Round(new MacroTotals(
            logged.Average(m => m.Calories), logged.Average(m => m.ProteinG), logged.Average(m => m.CarbsG), logged.Average(m => m.FatG)));
        var goal = await ActiveGoalAsync(userId);
        return new WeeklySummary(days, averages, logged.Count, goal is null ? null : Math.Round(goal.TargetCalories), await StreakAsync(userId));
    }

    // Días seguidos con comidas hasta hoy del usuario. Si hoy todavía no tiene, cuenta desde ayer:
    // la racha no se corta en medio del día.
    private async Task<int> StreakAsync(int userId)
    {
        var today = await userToday.GetAsync(userId);
        var dates = await store.GetLoggedDatesAsync(userId, today, MaxStreakDays);
        var start = dates.FirstOrDefault() == today ? today : today.AddDays(-1);
        var streak = 0;
        foreach (var date in dates)
        {
            if (date != start.AddDays(-streak)) break;
            streak++;
        }
        return streak;
    }

    private async Task<DateOnly> DayAsync(int userId, DateOnly? date, string paramName)
    {
        if (date is not { } day) return await userToday.GetAsync(userId);
        await userToday.ValidateDiaryDateAsync(userId, day, paramName);
        return day;
    }

    // Un objetivo que no se puede leer (sin perfil o sin peso) se muestra como "sin objetivo": el resumen no falla.
    private async Task<Goal?> ActiveGoalAsync(int userId)
    {
        try
        {
            return (await goalManager.GetTargetsAsync(userId))?.Goal;
        }
        catch (ConflictException)
        {
            return null;
        }
    }

    private static MacroTotals ToMacros(MealTotals t) => new(t.Calories, t.Protein, t.Carbs, t.Fat);

    private static MacroTotals Round(MacroTotals m) =>
        new(Math.Round(m.Calories), Math.Round(m.ProteinG, 1), Math.Round(m.CarbsG, 1), Math.Round(m.FatG, 1));

    private static MacroLeft Left(decimal target, decimal consumed, int decimals) =>
        new(Math.Round(target - consumed, decimals), consumed > target);

    // Un target en 0 no tiene porcentaje con sentido: queda en 0.
    private static decimal Pct(decimal consumed, decimal target) => target > 0 ? Math.Round(consumed / target * 100, 1) : 0;
}
