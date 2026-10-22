using AppNutricion.DataAccess.DataBaseContext;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;

namespace AppNutricion.DataAccess.Stores;

public class AssistantStore(ApplicationDbContext context) : IAssistantStore
{
    public Task AddConversationAsync(Conversation conversation) => throw new NotImplementedException();

    public Task<List<Conversation>> ListConversationsAsync(int userId) => throw new NotImplementedException();

    public Task<Conversation?> GetConversationAsync(int id, int userId) => throw new NotImplementedException();

    public Task<List<ChatMessage>> GetRecentMessagesAsync(int conversationId, int userId, int count) => throw new NotImplementedException();

    public Task<List<ChatMessage>> GetVisibleMessagesAsync(int conversationId, int userId) => throw new NotImplementedException();

    public Task<List<AssistantSuggestion>> GetSuggestionsAsync(int conversationId, int userId) => throw new NotImplementedException();

    public Task AddMessagesAsync(IEnumerable<ChatMessage> messages) => throw new NotImplementedException();

    public Task TouchConversationAsync(int id, int userId, string title, DateTime updatedAt) => throw new NotImplementedException();

    public Task AddSuggestionsAsync(IEnumerable<AssistantSuggestion> suggestions) => throw new NotImplementedException();

    public Task<AssistantSuggestion?> GetSuggestionAsync(int id, int userId) => throw new NotImplementedException();

    public Task<bool> ResolveSuggestionAsync(int id, int userId, SuggestionStatus status, DateTime resolvedAt, int? mealEntryId) =>
        throw new NotImplementedException();
}
