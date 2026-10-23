using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.BusinessLogic.Options;
using AppNutricion.Entities.Enums;
using Microsoft.Extensions.Logging;

namespace AppNutricion.BusinessLogic.Services;

// Chat Completions de cualquier proveedor compatible con OpenAI (D24: Gemini). Cambiar de proveedor = BaseUrl + ApiKey + Model.
// Un HTTP no 2xx, un timeout o un JSON inválido salen como excepción: AssistantManager responde degradado.
// Es singleton (como IChatModel): pide el HttpClient a IHttpClientFactory en cada llamada para no quedarse con uno viejo.
public class OpenAiCompatibleChatModel(IHttpClientFactory httpClients, LlmOptions options, ILogger<OpenAiCompatibleChatModel> logger) : IChatModel
{
    public const string HttpClientName = "Llm";

    public async Task<ChatCompletion> CompleteAsync(string systemPrompt, IReadOnlyList<ChatTurn> messages, IReadOnlyList<ChatToolDefinition> tools,
        CancellationToken cancellationToken)
    {
        var body = new JsonObject
        {
            ["model"] = options.Model,
            ["messages"] = new JsonArray([new JsonObject { ["role"] = "system", ["content"] = systemPrompt }, .. messages.Select(ToMessage)]),
            ["tools"] = new JsonArray([.. tools.Select(ToTool)]),
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(options.BaseUrl!.TrimEnd('/') + "/"), "chat/completions"))
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

        // Solo status y duración: el body lleva los totales del usuario.
        var started = Stopwatch.GetTimestamp();
        try
        {
            using var response = await httpClients.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
            logger.LogInformation("Llm: {Status} en {ElapsedMs:0} ms", (int)response.StatusCode, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            response.EnsureSuccessStatusCode();
            var completion = await response.Content.ReadFromJsonAsync<Response>(cancellationToken);
            var message = completion?.Choices?.FirstOrDefault()?.Message ?? throw new JsonException("Respuesta del LLM sin choices[0].message");
            return new ChatCompletion(message.Content, [.. (message.ToolCalls ?? []).Select((c, i) =>
                new ChatToolCall(c.Id ?? $"call-{i}", c.Function.Name, c.Function.Arguments ?? "{}", c.ExtraContent?.GetRawText()))]);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Llm: falló tras {ElapsedMs:0} ms ({Error})", Stopwatch.GetElapsedTime(started).TotalMilliseconds, ex.GetType().Name);
            throw;
        }
    }

    private static JsonObject ToMessage(ChatTurn turn) => turn.Role switch
    {
        ChatRole.Tool => new JsonObject { ["role"] = "tool", ["tool_call_id"] = turn.ToolCallId, ["content"] = turn.Content },
        ChatRole.Assistant when turn.ToolCalls is { Count: > 0 } calls => new JsonObject
        {
            ["role"] = "assistant",
            ["content"] = string.IsNullOrEmpty(turn.Content) ? null : turn.Content,
            ["tool_calls"] = new JsonArray([.. calls.Select(ToToolCall)]),
        },
        ChatRole.Assistant => new JsonObject { ["role"] = "assistant", ["content"] = turn.Content },
        _ => new JsonObject { ["role"] = "user", ["content"] = turn.Content },
    };

    private static JsonObject ToToolCall(ChatToolCall call)
    {
        var json = new JsonObject
        {
            ["id"] = call.Id,
            ["type"] = "function",
            ["function"] = new JsonObject { ["name"] = call.Name, ["arguments"] = call.ArgumentsJson },
        };
        // Gemini 3 exige que la firma de pensamiento (extra_content.google.thought_signature) vuelva tal como llegó.
        if (call.ProviderMetadataJson is not null) json["extra_content"] = JsonNode.Parse(call.ProviderMetadataJson);
        return json;
    }

    private static JsonObject ToTool(ChatToolDefinition tool) => new()
    {
        ["type"] = "function",
        ["function"] = new JsonObject
        {
            ["name"] = tool.Name, ["description"] = tool.Description, ["parameters"] = JsonNode.Parse(tool.ParametersJsonSchema),
        },
    };

    private record Response([property: JsonPropertyName("choices")] List<Choice>? Choices);

    private record Choice([property: JsonPropertyName("message")] Message? Message);

    private record Message(
        [property: JsonPropertyName("content")] string? Content,
        [property: JsonPropertyName("tool_calls")] List<ToolCall>? ToolCalls);

    private record ToolCall(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("function")] FunctionCall Function,
        [property: JsonPropertyName("extra_content")] JsonElement? ExtraContent);

    private record FunctionCall(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("arguments")] string? Arguments);
}
