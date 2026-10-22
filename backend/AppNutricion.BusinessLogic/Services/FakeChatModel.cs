using AppNutricion.BusinessLogic.Interfaces;

namespace AppNutricion.BusinessLogic.Services;

// Modelo guionado y determinístico: para los tests y como modelo cuando no hay clave del proveedor real.
public class FakeChatModel : IChatModel
{
    public Task<ChatCompletion> CompleteAsync(string systemPrompt, IReadOnlyList<ChatTurn> messages, IReadOnlyList<ChatToolDefinition> tools,
        CancellationToken cancellationToken) => throw new NotImplementedException();
}
