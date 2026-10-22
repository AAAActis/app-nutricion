using AppNutricion.Entities.Entities;

namespace AppNutricion.BusinessLogic.Interfaces;

// Degraded = el LLM falló o tardó demasiado y la respuesta es el mensaje de respaldo con los totales de hoy.
public record AssistantReply(ChatMessage Message, List<AssistantSuggestion> Suggestions, bool Degraded);

// Mensajes User y Assistant (los Tool no se muestran) y las sugerencias de la conversación, con su alimento.
public record ConversationHistory(List<ChatMessage> Messages, List<AssistantSuggestion> Suggestions);

// D5: el asistente solo PROPONE ingestas (AssistantSuggestion Pending); la comida se crea al aceptar, con
// MealEntryManager (macros de Foods, D6). Dato inválido → ArgumentException con el campo.
// Conversación o sugerencia de otro usuario → null. Sugerencia ya resuelta o alimento que ya no está → ConflictException.
public interface IAssistantManager
{
    Task<Conversation> CreateConversationAsync(int userId);
    Task<List<Conversation>> ListConversationsAsync(int userId);
    Task<ConversationHistory?> GetHistoryAsync(int conversationId, int userId);

    // content de 1 a 1000 caracteres. Nunca falla por culpa del LLM: si tira excepción o se pasa de Llm:TimeoutSeconds,
    // responde con Degraded = true.
    Task<AssistantReply?> SendAsync(int userId, int conversationId, string? content, CancellationToken cancellationToken = default);

    Task<AssistantSuggestion?> AcceptSuggestionAsync(int id, int userId);
    Task<AssistantSuggestion?> IgnoreSuggestionAsync(int id, int userId);
}
