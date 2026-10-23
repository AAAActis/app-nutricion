using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace AppNutricion.BusinessLogic.Options;

// Sección "Llm". ApiKey va en user-secrets (Llm__ApiKey en .env). Provider "Fake" = FakeChatModel (sin clave).
public class LlmOptions
{
    public const string SectionName = "Llm";
    public const string FakeProvider = "Fake";
    public const double DefaultTimeoutSeconds = 10;

    public string Provider { get; init; } = FakeProvider;
    public string? ApiKey { get; init; }
    public string? Model { get; init; }
    public double TimeoutSeconds { get; init; } = DefaultTimeoutSeconds;

    // Se lee a mano para no sumar el paquete del binder a BusinessLogic.
    public static LlmOptions From(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);
        return new LlmOptions
        {
            Provider = string.IsNullOrWhiteSpace(section["Provider"]) ? FakeProvider : section["Provider"]!.Trim(),
            ApiKey = string.IsNullOrWhiteSpace(section["ApiKey"]) ? null : section["ApiKey"],
            Model = section["Model"],
            TimeoutSeconds = double.TryParse(section["TimeoutSeconds"], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds > 0
                ? seconds
                : DefaultTimeoutSeconds,
        };
    }
}
