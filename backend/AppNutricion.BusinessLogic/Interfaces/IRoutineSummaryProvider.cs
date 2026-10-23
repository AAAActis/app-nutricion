namespace AppNutricion.BusinessLogic.Interfaces;

public record RoutineExerciseSummary(string Name, string MuscleGroup, int Sets, int Reps, decimal? WeightKg);

public record RoutineDaySummary(int DayNumber, string Name, List<RoutineExerciseSummary> Exercises);

// Sin la descripción: es texto libre del usuario y al LLM solo va el contexto permitido (D23-asistente).
public record RoutineSummary(string Name, bool IsPreset, List<RoutineDaySummary> Days);

// Rutinas visibles para el usuario (precargadas + propias, nunca las de otro), con días y ejercicios, para la
// herramienta consultar_rutinas y el contexto del asistente.
public interface IRoutineSummaryProvider
{
    Task<List<RoutineSummary>> GetAsync(int userId);
}
