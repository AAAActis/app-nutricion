using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.Entities.Entities;

namespace AppNutricion.Presentation.Output;

public record ConversationOutput(int Id, string? Title, DateTime CreatedAt, DateTime UpdatedAt)
{
    public static ConversationOutput From(Conversation c) => new(c.Id, c.Title, c.CreatedAt, c.UpdatedAt);
}

public record SuggestionOutput(
    int Id, int ChatMessageId, int FoodId, string FoodName, decimal QuantityGrams, string MealType, DateOnly Date, string Status,
    int? MealEntryId, DateTime CreatedAt, DateTime? ResolvedAt)
{
    public static SuggestionOutput From(AssistantSuggestion s) => new(
        s.Id, s.ChatMessageId, s.FoodId, s.Food.Name, s.QuantityGrams, s.MealType.ToString(), s.Date, s.Status.ToString(),
        s.MealEntryId, s.CreatedAt, s.ResolvedAt);
}

public record ChatMessageOutput(int Id, string Role, string Content, DateTime CreatedAt, List<SuggestionOutput> Suggestions)
{
    public static List<ChatMessageOutput> From(ConversationHistory history) => history.Messages
        .Select(m => new ChatMessageOutput(m.Id, m.Role.ToString(), m.Content, m.CreatedAt,
            history.Suggestions.Where(s => s.ChatMessageId == m.Id).Select(SuggestionOutput.From).ToList()))
        .ToList();
}

public record AssistantMessageOutput(int Id, string Content, DateTime CreatedAt);

// Degraded = el LLM no respondió y el texto es el de respaldo con los totales de hoy.
public record AssistantReplyOutput(AssistantMessageOutput Message, List<SuggestionOutput> Suggestions, bool Degraded)
{
    public static AssistantReplyOutput From(AssistantReply r) => new(
        new AssistantMessageOutput(r.Message.Id, r.Message.Content, r.Message.CreatedAt),
        r.Suggestions.Select(SuggestionOutput.From).ToList(), r.Degraded);
}
