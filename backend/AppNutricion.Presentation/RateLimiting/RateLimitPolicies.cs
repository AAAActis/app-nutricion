using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AppNutricion.Presentation.RateLimiting;

// Rate limiting (J6) con el de ASP.NET Core, sin paquetes. Los valores por defecto están en "Decisiones a
// ratificar" del PR y se cambian solo acá (o por configuración, sección RateLimiting).
public static class RateLimitPolicies
{
    public const string ConfigSection = "RateLimiting";

    // Login y registro: ventana fija por IP.
    public const string Auth = "auth";
    public const int AuthPermitLimitDefault = 5;
    public static readonly TimeSpan AuthWindow = TimeSpan.FromMinutes(1);

    // Chat con el asistente (S9): ventana fija por usuario (o por IP si no hay token). Lista, sin aplicar.
    public const string AssistantChat = "assistant-chat";
    public const int AssistantChatPermitLimitDefault = 20;
    public static readonly TimeSpan AssistantChatWindow = TimeSpan.FromMinutes(1);

    // Stub: todavía no registra nada.
    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services, IConfiguration configuration) => services;
}
