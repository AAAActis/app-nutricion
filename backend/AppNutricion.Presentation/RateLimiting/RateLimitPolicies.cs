using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
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

    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(ConfigSection);
        var authLimit = section.GetValue("AuthPermitLimit", AuthPermitLimitDefault);
        var chatLimit = section.GetValue("AssistantChatPermitLimit", AssistantChatPermitLimitDefault);

        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(Auth, context => RateLimitPartition.GetFixedWindowLimiter(
                ClientIp(context), _ => FixedWindow(authLimit, AuthWindow)));

            // Con token, por usuario ("sub"); sin token, por IP.
            options.AddPolicy(AssistantChat, context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirst("sub")?.Value is { } sub ? $"user:{sub}" : $"ip:{ClientIp(context)}",
                _ => FixedWindow(chatLimit, AssistantChatWindow)));

            // 429 como ProblemDetails, con Retry-After en segundos.
            options.OnRejected = async (rejected, cancellationToken) =>
            {
                var http = rejected.HttpContext;
                if (rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    http.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                await http.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = http,
                    ProblemDetails = { Status = StatusCodes.Status429TooManyRequests, Title = "Demasiados intentos. Probá de nuevo en un rato." },
                });
            };
        });
    }

    // Sin proxy configurado, la IP es la de la conexión. Detrás de un proxy (Docker, nginx) serían todas la misma:
    // hay que sumar ForwardedHeaders (pendiente, ver PR).
    private static string ClientIp(HttpContext context) => context.Connection.RemoteIpAddress?.ToString() ?? "desconocida";

    private static FixedWindowRateLimiterOptions FixedWindow(int permitLimit, TimeSpan window) => new()
    {
        PermitLimit = permitLimit,
        Window = window,
        QueueLimit = 0,
    };
}
