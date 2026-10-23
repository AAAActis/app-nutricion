using AppNutricion.BusinessLogic.Interfaces;

namespace AppNutricion.Presentation.Input;

// Sin UserId a propósito: sale del token. Días y ejercicios con la forma de RoutineDayDraft / RoutineExerciseDraft.
public record CreateRoutineInput(string? Name, string? Description, List<RoutineDayDraft>? Days);
