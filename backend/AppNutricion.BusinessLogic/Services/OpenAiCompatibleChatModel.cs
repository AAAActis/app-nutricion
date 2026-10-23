using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.BusinessLogic.Options;
using Microsoft.Extensions.Logging;

namespace AppNutricion.BusinessLogic.Services;

public class OpenAiCompatibleChatModel(IHttpClientFactory httpClients, LlmOptions options, ILogger<OpenAiCompatibleChatModel> logger) : IChatModel
{
    public Task<ChatCompletion> CompleteAsync(string systemPrompt, IReadOnlyList<ChatTurn> messages, IReadOnlyList<ChatToolDefinition> tools,
        CancellationToken cancellationToken) => throw new NotImplementedException();
}
