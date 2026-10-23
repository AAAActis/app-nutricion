using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;

namespace AppNutricion.DataAccess.Interfaces;

// Conversaciones, mensajes y sugerencias del asistente. Todo filtra por userId: lo de otro usuario no existe (null/false/vacío).
public interface IAssistantStore
{
    Task AddConversationAsync(Conversation conversation);

    // Del más reciente (UpdatedAt) al más viejo.
    Task<List<Conversation>> ListConversationsAsync(int userId);

    Task<Conversation?> GetConversationAsync(int id, int userId);

    // Los últimos "count" mensajes User y Assistant, del más viejo al más nuevo (los Tool no van al historial del modelo).
    Task<List<ChatMessage>> GetRecentMessagesAsync(int conversationId, int userId, int count);

    // Todos los mensajes User y Assistant, en orden.
    Task<List<ChatMessage>> GetVisibleMessagesAsync(int conversationId, int userId);

    // Las sugerencias de la conversación, con su Food.
    Task<List<AssistantSuggestion>> GetSuggestionsAsync(int conversationId, int userId);

    Task AddMessagesAsync(IEnumerable<ChatMessage> messages);

    // UpdatedAt = updatedAt; Title = title solo si todavía no tiene.
    Task TouchConversationAsync(int id, int userId, string title, DateTime updatedAt);

    Task AddSuggestionsAsync(IEnumerable<AssistantSuggestion> suggestions);

    // Con su Food.
    Task<AssistantSuggestion?> GetSuggestionAsync(int id, int userId);

    // Solo si sigue Pending (dos aceptaciones a la vez: gana una). false = no existe, es de otro o ya estaba resuelta.
    Task<bool> ResolveSuggestionAsync(int id, int userId, SuggestionStatus status, DateTime resolvedAt, int? mealEntryId);
}
