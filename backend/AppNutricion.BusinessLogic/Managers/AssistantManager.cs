using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.BusinessLogic.Options;
using AppNutricion.BusinessLogic.Services;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;

namespace AppNutricion.BusinessLogic.Managers;

public class AssistantManager(
    IAssistantStore store, IChatModel chatModel, AssistantTools tools, IMealEntryManager mealManager, ISummaryManager summaryManager,
    IGoalManager goalManager, IRoutineSummaryProvider routines, LlmOptions llmOptions, TimeProvider timeProvider) : IAssistantManager
{
    public Task<Conversation> CreateConversationAsync(int userId) => throw new NotImplementedException();

    public Task<List<Conversation>> ListConversationsAsync(int userId) => throw new NotImplementedException();

    public Task<ConversationHistory?> GetHistoryAsync(int conversationId, int userId) => throw new NotImplementedException();

    public Task<AssistantReply?> SendAsync(int userId, int conversationId, string? content, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<AssistantSuggestion?> AcceptSuggestionAsync(int id, int userId) => throw new NotImplementedException();

    public Task<AssistantSuggestion?> IgnoreSuggestionAsync(int id, int userId) => throw new NotImplementedException();
}
