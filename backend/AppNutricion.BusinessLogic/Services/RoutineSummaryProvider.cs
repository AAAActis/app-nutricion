using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;

namespace AppNutricion.BusinessLogic.Services;

// Las rutinas visibles (precargadas + propias) desde IRoutineStore, que ya filtra por userId. La lista no trae
// días: se pide cada rutina con GetVisibleAsync (una consulta por rutina; son pocas).
public class RoutineSummaryProvider(IRoutineStore store) : IRoutineSummaryProvider
{
    public async Task<List<RoutineSummary>> GetAsync(int userId)
    {
        var summaries = new List<RoutineSummary>();
        foreach (var routine in await store.ListVisibleAsync(userId))
        {
            if (await store.GetVisibleAsync(routine.Id, userId) is { } detail) summaries.Add(Summary(detail));
        }
        return summaries;
    }

    private static RoutineSummary Summary(Routine routine) => new(
        routine.Name, routine.UserId is null,
        routine.Days.Select(d => new RoutineDaySummary(d.DayNumber, d.Name,
            d.Exercises.Select(e => new RoutineExerciseSummary(e.Exercise.Name, e.Exercise.MuscleGroup.ToString(), e.TargetSets, e.TargetReps, e.TargetWeightKg))
                .ToList())).ToList());
}
