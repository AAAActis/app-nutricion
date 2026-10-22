using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.Entities.Enums;

namespace AppNutricion.BusinessLogic.Services;

// Una ingesta que el modelo propuso y pasó las validaciones. Se guarda como AssistantSuggestion Pending.
public record ProposedIntake(int FoodId, string FoodName, decimal QuantityGrams, MealType MealType, DateOnly Date);

// Result es el JSON que vuelve al modelo ({"error": "..."} si algo no valida).
public record ToolOutcome(string Result, ProposedIntake? Proposal);

// Las herramientas del asistente. Cada una delega en un manager (nunca en un store) y valida los argumentos del LLM
// como si vinieran de un cliente. El userId lo pone la API, nunca el modelo.
public class AssistantTools(IFoodManager foodManager, ISummaryManager summaryManager, IRoutineSummaryProvider routines, IUserToday userToday)
{
    public const string SearchFood = "buscar_alimento";
    public const string DailyTotals = "totales_del_dia";
    public const string ListRoutines = "consultar_rutinas";
    public const string ProposeIntake = "proponer_ingesta";

    public IReadOnlyList<ChatToolDefinition> Definitions => throw new NotImplementedException();

    public Task<ToolOutcome> ExecuteAsync(int userId, ChatToolCall call) => throw new NotImplementedException();
}
