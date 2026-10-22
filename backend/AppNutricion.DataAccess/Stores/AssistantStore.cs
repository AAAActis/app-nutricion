using AppNutricion.DataAccess.DataBaseContext;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;
using Microsoft.EntityFrameworkCore;

namespace AppNutricion.DataAccess.Stores;

public class AssistantStore(ApplicationDbContext context) : IAssistantStore
{
    public async Task AddConversationAsync(Conversation conversation)
    {
        context.Conversations.Add(conversation);
        await context.SaveChangesAsync();
    }

    public Task<List<Conversation>> ListConversationsAsync(int userId) =>
        Conversations(userId).OrderByDescending(c => c.UpdatedAt).ThenByDescending(c => c.Id).ToListAsync();

    public Task<Conversation?> GetConversationAsync(int id, int userId) =>
        Conversations(userId).FirstOrDefaultAsync(c => c.Id == id);

    public async Task<List<ChatMessage>> GetRecentMessagesAsync(int conversationId, int userId, int count)
    {
        var latest = await VisibleMessages(conversationId, userId)
            .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id).Take(count).ToListAsync();
        latest.Reverse();
        return latest;
    }

    public Task<List<ChatMessage>> GetVisibleMessagesAsync(int conversationId, int userId) =>
        VisibleMessages(conversationId, userId).OrderBy(m => m.CreatedAt).ThenBy(m => m.Id).ToListAsync();

    public Task<List<AssistantSuggestion>> GetSuggestionsAsync(int conversationId, int userId) =>
        context.AssistantSuggestions.AsNoTracking().Include(s => s.Food)
            .Where(s => s.UserId == userId
                        && context.ChatMessages.Any(m => m.Id == s.ChatMessageId && m.ConversationId == conversationId))
            .OrderBy(s => s.Id).ToListAsync();

    public async Task AddMessagesAsync(IEnumerable<ChatMessage> messages)
    {
        context.ChatMessages.AddRange(messages);
        await context.SaveChangesAsync();
    }

    public async Task TouchConversationAsync(int id, int userId, string title, DateTime updatedAt) =>
        await context.Conversations.Where(c => c.Id == id && c.UserId == userId).ExecuteUpdateAsync(s => s
            .SetProperty(c => c.Title, c => c.Title ?? title)
            .SetProperty(c => c.UpdatedAt, updatedAt));

    public async Task AddSuggestionsAsync(IEnumerable<AssistantSuggestion> suggestions)
    {
        context.AssistantSuggestions.AddRange(suggestions);
        await context.SaveChangesAsync();
    }

    public Task<AssistantSuggestion?> GetSuggestionAsync(int id, int userId) =>
        context.AssistantSuggestions.AsNoTracking().Include(s => s.Food).FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

    public async Task<bool> ResolveSuggestionAsync(int id, int userId, SuggestionStatus status, DateTime resolvedAt, int? mealEntryId) =>
        await context.AssistantSuggestions
            .Where(s => s.Id == id && s.UserId == userId && s.Status == SuggestionStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, status)
                .SetProperty(x => x.ResolvedAt, resolvedAt)
                .SetProperty(x => x.MealEntryId, mealEntryId)) > 0;

    private IQueryable<Conversation> Conversations(int userId) =>
        context.Conversations.AsNoTracking().Where(c => c.UserId == userId);

    private IQueryable<ChatMessage> VisibleMessages(int conversationId, int userId) =>
        context.ChatMessages.AsNoTracking()
            .Where(m => m.ConversationId == conversationId && m.Role != ChatRole.Tool
                        && context.Conversations.Any(c => c.Id == conversationId && c.UserId == userId));
}
