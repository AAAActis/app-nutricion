using AppNutricion.BusinessLogic.Interfaces;

namespace AppNutricion.BusinessLogic.Services;

// shortcut: siempre vacío porque IRoutineManager todavía no está en develop; al entrar el #16 se reemplaza por uno que lo use.
public class EmptyRoutineSummaryProvider : IRoutineSummaryProvider
{
    public Task<List<RoutineSummary>> GetAsync(int userId) => Task.FromResult(new List<RoutineSummary>());
}
