namespace AppNutricion.BusinessLogic.Interfaces;

public record RoutineSummary(string Name, List<string> Days);

// Rutinas visibles para el usuario (precargadas + propias), para la herramienta consultar_rutinas y el contexto
// del asistente. Hasta que entre el #16 (rutinas) la implementación es EmptyRoutineSummaryProvider; después se
// conecta a IRoutineManager.
public interface IRoutineSummaryProvider
{
    Task<List<RoutineSummary>> GetAsync(int userId);
}
