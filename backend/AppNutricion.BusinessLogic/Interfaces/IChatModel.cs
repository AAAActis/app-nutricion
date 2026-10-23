using AppNutricion.Entities.Enums;

namespace AppNutricion.BusinessLogic.Interfaces;

// Una herramienta que el modelo puede pedir: nombre, para qué sirve y sus parámetros como JSON Schema.
public record ChatToolDefinition(string Name, string Description, string ParametersJsonSchema);

// Un pedido del modelo de ejecutar una herramienta. ArgumentsJson lo arma el LLM: se valida como si viniera de un cliente.
public record ChatToolCall(string Id, string Name, string ArgumentsJson, string? ProviderMetadataJson = null);

// Un turno de la charla que se le manda al modelo. El del asistente puede traer ToolCalls; el de una
// herramienta trae el ToolCallId que responde.
public record ChatTurn(ChatRole Role, string Content, IReadOnlyList<ChatToolCall>? ToolCalls = null, string? ToolCallId = null);

// Texto y/o pedidos de herramientas (ToolCalls vacío = respuesta final).
public record ChatCompletion(string? Text, IReadOnlyList<ChatToolCall> ToolCalls);

// El LLM detrás de una interfaz (S9). Nunca recibe userId, email ni nombre: solo el prompt de sistema con el
// contexto permitido y los turnos de la conversación.
public interface IChatModel
{
    Task<ChatCompletion> CompleteAsync(string systemPrompt, IReadOnlyList<ChatTurn> messages, IReadOnlyList<ChatToolDefinition> tools,
        CancellationToken cancellationToken);
}
