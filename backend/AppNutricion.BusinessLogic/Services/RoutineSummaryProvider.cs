using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.DataAccess.Interfaces;

namespace AppNutricion.BusinessLogic.Services;

public class RoutineSummaryProvider(IRoutineStore store) : IRoutineSummaryProvider
{
    public IRoutineStore Store { get; } = store;

    public Task<List<RoutineSummary>> GetAsync(int userId) => Task.FromResult(new List<RoutineSummary>());
}
