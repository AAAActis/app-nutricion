using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.Entities.Enums;
using AppNutricion.Entities.Helpers;

namespace AppNutricion.BusinessLogic.Services;

// Modelo guionado y determinístico: para los tests y como modelo cuando no hay clave del proveedor real.
// "comí 150 g de arroz" → buscar_alimento("arroz") → proponer_ingesta(primer resultado, 150 g, Lunch).
// "cuánto me falta" → totales_del_dia. Cualquier otra cosa → respuesta genérica.
public partial class FakeChatModel : IChatModel
{
    private const decimal DefaultGrams = 100;
    private const string GenericReply =
        "Puedo ayudarte a registrar lo que comiste (por ejemplo: \"comí 150 g de arroz\") o decirte cuánto te falta hoy. Las cifras son estimaciones.";

    [GeneratedRegex(@"\bcomi\b\s*(?:(?<g>\d+(?:[.,]\d+)?)\s*(?:g|gr|grs|gramos)\b\s*)?(?:de\s+)?(?<term>.*)")]
    private static partial Regex AteRegex();

    public Task<ChatCompletion> CompleteAsync(string systemPrompt, IReadOnlyList<ChatTurn> messages, IReadOnlyList<ChatToolDefinition> tools,
        CancellationToken cancellationToken)
    {
        var last = messages[^1];
        return Task.FromResult(last.Role == ChatRole.Tool ? AfterTool(messages) : AfterUser(last.Content));
    }

    private static ChatCompletion AfterUser(string text)
    {
        var normalized = TextNormalizer.Normalize(text);
        var ate = AteRegex().Match(normalized);
        var term = ate.Success ? ate.Groups["term"].Value.Trim(' ', '.', '!', '?') : "";
        if (term.Length > 0) return Call(AssistantTools.SearchFood, new { q = term });
        if (normalized.Contains("cuanto me falta")) return Call(AssistantTools.DailyTotals, new { });
        return Reply(GenericReply);
    }

    private static ChatCompletion AfterTool(IReadOnlyList<ChatTurn> messages)
    {
        var result = messages[^1];
        var request = messages.LastOrDefault(m => m.ToolCalls?.Any(c => c.Id == result.ToolCallId) == true);
        var tool = request?.ToolCalls!.First(c => c.Id == result.ToolCallId).Name;
        var json = JsonDocument.Parse(result.Content).RootElement;
        if (json.TryGetProperty("error", out var error)) return Reply($"No pude completar eso: {error.GetString()}");

        switch (tool)
        {
            case AssistantTools.SearchFood:
                var found = json.GetProperty("resultados");
                if (found.GetArrayLength() == 0) return Reply("No encontré ese alimento en la base. Probá con otro nombre.");
                var userText = TextNormalizer.Normalize(messages.Last(m => m.Role == ChatRole.User).Content);
                var grams = AteRegex().Match(userText).Groups["g"] is { Success: true } g
                    ? decimal.Parse(g.Value.Replace(',', '.'), CultureInfo.InvariantCulture)
                    : DefaultGrams;
                return Call(AssistantTools.ProposeIntake, new { foodId = found[0].GetProperty("id").GetInt32(), gramos = grams, mealType = nameof(MealType.Lunch) });
            case AssistantTools.ProposeIntake:
                return Reply($"Te propuse registrar {json.GetProperty("gramos").GetDecimal().ToString(CultureInfo.InvariantCulture)} g de " +
                             $"{json.GetProperty("alimento").GetString()}. Confirmala o ignorala; los valores son una estimación.");
            case AssistantTools.DailyTotals:
                var eaten = Kcal(json.GetProperty("consumido"));
                if (json.GetProperty("objetivo").ValueKind == JsonValueKind.Null) return Reply($"Hoy llevás {eaten} kcal (estimación).");
                return Reply($"Hoy llevás {eaten} kcal de {Kcal(json.GetProperty("objetivo"))}: te faltan {Kcal(json.GetProperty("restante"))} kcal (estimación).");
            default:
                return Reply(GenericReply);
        }
    }

    private static string Kcal(JsonElement macros) => macros.GetProperty("kcal").GetDecimal().ToString("0", CultureInfo.InvariantCulture);

    private static ChatCompletion Reply(string text) => new(text, []);

    private static ChatCompletion Call(string tool, object args) =>
        new(null, [new ChatToolCall($"fake-{tool}", tool, JsonSerializer.Serialize(args))]);
}
