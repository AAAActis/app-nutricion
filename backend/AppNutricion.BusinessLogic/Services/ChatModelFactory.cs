using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.BusinessLogic.Options;
using Microsoft.Extensions.Logging;

namespace AppNutricion.BusinessLogic.Services;

// Elige el IChatModel según Llm:Provider.
public class ChatModelFactory
{
    public static IChatModel Create(LlmOptions options, ILogger<ChatModelFactory> logger) => throw new NotImplementedException();
}
