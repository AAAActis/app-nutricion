using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.BusinessLogic.Options;
using Microsoft.Extensions.Logging;

namespace AppNutricion.BusinessLogic.Services;

// Elige el IChatModel según Llm:Provider. Sin clave, BaseUrl o Model (o con un proveedor sin adaptador) avisa y usa el Fake:
// la API arranca igual.
public class ChatModelFactory
{
    public static IChatModel Create(LlmOptions options, ILogger<ChatModelFactory> logger, Func<IChatModel> openAiCompatible)
    {
        if (options.Provider.Equals(LlmOptions.FakeProvider, StringComparison.OrdinalIgnoreCase)) return new FakeChatModel();
        if (options.ApiKey is null || options.BaseUrl is null || options.Model is null)
        {
            logger.LogWarning("Llm: Provider {Provider} sin ApiKey (user-secrets Llm:ApiKey), BaseUrl o Model; se usa el modelo Fake", options.Provider);
            return new FakeChatModel();
        }
        if (options.Provider.Equals(LlmOptions.OpenAiCompatibleProvider, StringComparison.OrdinalIgnoreCase)) return openAiCompatible();
        logger.LogWarning("Llm: no hay adaptador para {Provider} (usá {OpenAiCompatible}); se usa el modelo Fake",
            options.Provider, LlmOptions.OpenAiCompatibleProvider);
        return new FakeChatModel();
    }
}
