using AppNutricion.BusinessLogic.Interfaces;

namespace AppNutricion.Presentation.Input;

// Sin UserId a propósito: sale del token. weightKg 0 = peso corporal o cardio.
public record CreateWorkoutInput(int? RoutineDayId, DateOnly Date, int DurationMinutes, string? Notes, List<WorkoutSetDraft>? Sets);
