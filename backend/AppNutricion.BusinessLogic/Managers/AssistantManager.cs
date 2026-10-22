using System.Globalization;
using System.Text;
using AppNutricion.BusinessLogic.Exceptions;
using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.BusinessLogic.Options;
using AppNutricion.BusinessLogic.Services;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;

namespace AppNutricion.BusinessLogic.Managers;

public class AssistantManager(
    IAssistantStore store, IChatModel chatModel, AssistantTools tools, IMealEntryManager mealManager, ISummaryManager summaryManager,
    IGoalManager goalManager, IRoutineSummaryProvider routines, LlmOptions llmOptions, TimeProvider timeProvider) : IAssistantManager
{
    public const string AlreadyResolvedMessage = "La sugerencia ya fue resuelta.";
    public const string FoodGoneMessage = "El alimento ya no está disponible";
    private const int ContentMaxLength = 1000;
    private const int TitleMaxLength = 60;
    private const int HistoryMessages = 20;
    private const int MaxToolRounds = 3;
    private const string CutReply = "No pude terminar de procesar tu consulta. Probá reformularla en un mensaje más simple.";

    private const string Rules = """
        Sos el asistente nutricional de una app de registro de comidas y entrenamiento. Respondé en español rioplatense, breve y claro.
        Reglas:
        - No hacés diagnósticos médicos ni indicás tratamientos.
        - No recomendás déficit calórico a menores de 18 años ni dietas extremas.
        - Si la persona menciona síntomas (dolor, mareos, desmayos, conductas alimentarias de riesgo), sugerile consultar a un profesional de la salud.
        - Toda cifra de calorías o macros es una estimación: decilo.
        - Calorías y macros salen de las herramientas (buscar_alimento, totales_del_dia), nunca las inventes.
        - Para registrar una comida usá proponer_ingesta: queda como propuesta y la persona la confirma o la ignora.
        """;

    public async Task<Conversation> CreateConversationAsync(int userId)
    {
        var now = Now;
        var conversation = new Conversation { UserId = userId, CreatedAt = now, UpdatedAt = now };
        await store.AddConversationAsync(conversation);
        return conversation;
    }

    public Task<List<Conversation>> ListConversationsAsync(int userId) => store.ListConversationsAsync(userId);

    public async Task<ConversationHistory?> GetHistoryAsync(int conversationId, int userId)
    {
        if (await store.GetConversationAsync(conversationId, userId) is null) return null;
        return new ConversationHistory(
            await store.GetVisibleMessagesAsync(conversationId, userId),
            await store.GetSuggestionsAsync(conversationId, userId));
    }

    public async Task<AssistantReply?> SendAsync(int userId, int conversationId, string? content, CancellationToken cancellationToken = default)
    {
        var text = content?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length > ContentMaxLength)
            throw new ArgumentException($"El mensaje es obligatorio y de hasta {ContentMaxLength} caracteres.", "content");
        if (await store.GetConversationAsync(conversationId, userId) is null) return null;

        await store.AddMessagesAsync([new ChatMessage { ConversationId = conversationId, Role = ChatRole.User, Content = text, CreatedAt = Now }]);
        var today = await summaryManager.GetDailyAsync(userId, null);
        var prompt = await SystemPromptAsync(userId, today);
        var turns = (await store.GetRecentMessagesAsync(conversationId, userId, HistoryMessages))
            .Select(m => new ChatTurn(m.Role, m.Content)).ToList();

        var toolMessages = new List<ChatMessage>();
        var proposals = new List<ProposedIntake>();
        string reply;
        var degraded = false;
        // El presupuesto de tiempo es para todo el bucle; solo la llamada al modelo se abandona si se pasa
        // (las herramientas usan el DbContext del request y no pueden quedar corriendo en paralelo).
        var deadline = timeProvider.GetUtcNow() + TimeSpan.FromSeconds(llmOptions.TimeoutSeconds);
        using var llmCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            for (var round = 0; ; round++)
            {
                var remaining = deadline - timeProvider.GetUtcNow();
                if (remaining <= TimeSpan.Zero) throw new TimeoutException();
                var completion = await chatModel.CompleteAsync(prompt, turns.ToArray(), tools.Definitions, llmCancellation.Token)
                    .WaitAsync(remaining, timeProvider, cancellationToken);
                if (completion.ToolCalls.Count == 0 || round == MaxToolRounds)
                {
                    reply = string.IsNullOrWhiteSpace(completion.Text) || completion.ToolCalls.Count > 0 ? CutReply : completion.Text.Trim();
                    break;
                }

                turns.Add(new ChatTurn(ChatRole.Assistant, completion.Text ?? "", completion.ToolCalls));
                foreach (var call in completion.ToolCalls)
                {
                    var outcome = await tools.ExecuteAsync(userId, call);
                    if (outcome.Proposal is not null) proposals.Add(outcome.Proposal);
                    turns.Add(new ChatTurn(ChatRole.Tool, outcome.Result, ToolCallId: call.Id));
                    toolMessages.Add(new ChatMessage
                    {
                        ConversationId = conversationId, Role = ChatRole.Tool, Content = $"{call.Name}({call.ArgumentsJson}) → {outcome.Result}", CreatedAt = Now,
                    });
                }
            }
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Nunca un 500 por culpa del LLM (excepción o timeout): respaldo con los totales de hoy y sin propuestas.
            await llmCancellation.CancelAsync();
            reply = Fallback(today);
            degraded = true;
            proposals.Clear();
        }

        var assistant = new ChatMessage { ConversationId = conversationId, Role = ChatRole.Assistant, Content = reply, CreatedAt = Now };
        await store.AddMessagesAsync([.. toolMessages, assistant]);
        var suggestions = proposals.Select(p => new AssistantSuggestion
        {
            UserId = userId, ChatMessageId = assistant.Id, FoodId = p.FoodId, QuantityGrams = p.QuantityGrams, MealType = p.MealType,
            Date = p.Date, Status = SuggestionStatus.Pending, CreatedAt = Now,
        }).ToList();
        if (suggestions.Count > 0)
        {
            await store.AddSuggestionsAsync(suggestions);
            // Se releen para devolverlas con su alimento.
            suggestions = (await store.GetSuggestionsAsync(conversationId, userId)).Where(s => s.ChatMessageId == assistant.Id).ToList();
        }
        await store.TouchConversationAsync(conversationId, userId, text.Length > TitleMaxLength ? text[..TitleMaxLength] : text, Now);
        return new AssistantReply(assistant, suggestions, degraded);
    }

    public async Task<AssistantSuggestion?> AcceptSuggestionAsync(int id, int userId)
    {
        var suggestion = await PendingAsync(id, userId);
        if (suggestion is null) return null;

        // D6: la comida se crea con los macros de Foods (MealEntryManager), nunca con lo que haya dicho el LLM.
        var entry = await mealManager.CreateAsync(userId, suggestion.FoodId, suggestion.QuantityGrams, false, suggestion.MealType, suggestion.Date, null)
                    ?? throw new ConflictException(FoodGoneMessage);
        var now = Now;
        if (!await store.ResolveSuggestionAsync(id, userId, SuggestionStatus.Accepted, now, entry.Id))
        {
            // Otra aceptación ganó entre la lectura y el update: se deshace la comida para no duplicarla.
            await mealManager.DeleteAsync(entry.Id, userId);
            throw new ConflictException(AlreadyResolvedMessage);
        }
        suggestion.Status = SuggestionStatus.Accepted;
        suggestion.ResolvedAt = now;
        suggestion.MealEntryId = entry.Id;
        return suggestion;
    }

    public async Task<AssistantSuggestion?> IgnoreSuggestionAsync(int id, int userId)
    {
        var suggestion = await PendingAsync(id, userId);
        if (suggestion is null) return null;
        var now = Now;
        if (!await store.ResolveSuggestionAsync(id, userId, SuggestionStatus.Ignored, now, null))
            throw new ConflictException(AlreadyResolvedMessage);
        suggestion.Status = SuggestionStatus.Ignored;
        suggestion.ResolvedAt = now;
        return suggestion;
    }

    private DateTime Now => timeProvider.GetUtcNow().UtcDateTime;

    private async Task<AssistantSuggestion?> PendingAsync(int id, int userId)
    {
        var suggestion = await store.GetSuggestionAsync(id, userId);
        if (suggestion is { Status: not SuggestionStatus.Pending }) throw new ConflictException(AlreadyResolvedMessage);
        return suggestion;
    }

    // Solo el contexto permitido (D23): tipo de objetivo, kcal y macros, totales de hoy y nombres de rutinas.
    // Nunca userId, email ni nombre.
    private async Task<string> SystemPromptAsync(int userId, DailySummary today)
    {
        var goal = (await goalManager.ListAsync(userId)).FirstOrDefault(g => g.IsActive);
        var routineNames = (await routines.GetAsync(userId)).Select(r => r.Name).ToList();
        var prompt = new StringBuilder(Rules).AppendLine().AppendLine(CultureInfo.InvariantCulture, $"Contexto (hoy es {today.Date:yyyy-MM-dd}):");
        prompt.AppendLine(today.Targets is { } t
            ? Invariant($"- Objetivo{(goal is null ? "" : $" ({goal.GoalType})")}: {t.Calories} kcal, proteínas {t.ProteinG} g, carbohidratos {t.CarbsG} g, grasas {t.FatG} g.")
            : "- Sin objetivo activo.");
        var c = today.Consumed;
        prompt.AppendLine(Invariant($"- Lleva hoy: {c.Calories} kcal, proteínas {c.ProteinG} g, carbohidratos {c.CarbsG} g, grasas {c.FatG} g."));
        prompt.AppendLine(routineNames.Count > 0 ? $"- Rutinas: {string.Join("; ", routineNames)}." : "- Sin rutinas.");
        return prompt.ToString();
    }

    private static string Fallback(DailySummary today) =>
        "El asistente no está disponible en este momento. " + (today.Targets is { } t
            ? Invariant($"Hoy llevás {today.Consumed.Calories:0} kcal de {t.Calories:0}.")
            : Invariant($"Hoy llevás {today.Consumed.Calories:0} kcal."));

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
