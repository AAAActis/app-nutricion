using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.BusinessLogic.Options;
using Microsoft.Extensions.Logging;

namespace AppNutricion.BusinessLogic.Services;

// Elige el IChatModel según Llm:Provider. Sin clave (o sin adaptador todavía) avisa y usa el Fake: la API arranca igual.
public class ChatModelFactory
{
    public static IChatModel Create(LlmOptions options, ILogger<ChatModelFactory> logger, Func<IChatModel> openAiCompatible)
    {
        if (options.Provider.Equals(LlmOptions.FakeProvider, StringComparison.OrdinalIgnoreCase)) return new FakeChatModel();
        if (options.ApiKey is null)
        {
            logger.LogWarning("Llm: Provider {Provider} sin ApiKey (user-secrets Llm:ApiKey); se usa el modelo Fake", options.Provider);
            return new FakeChatModel();
        }
        // TODO(S9): adaptador del proveedor real (IChatModel que traduzca ChatTurn/ChatToolDefinition a su API, con options.Model).
        // El proveedor se elige cuando esté la clave; hasta entonces, el Fake.
        logger.LogWarning("Llm: todavía no hay adaptador para {Provider}; se usa el modelo Fake", options.Provider);
        return new FakeChatModel();
    }
}
