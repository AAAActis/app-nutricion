using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace AppNutricion.Tests.Infraestructura;

// API con usuarios en memoria que guarda TODO lo que se loguea (nivel Trace, todas las categorías, con excepciones).
public class ApiFactoryLogs : ApiFactoryAuth
{
    public LogsCapturados Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        // La regla propia del provider gana sobre las de appsettings (que dejan Microsoft.AspNetCore en Warning).
        builder.ConfigureLogging(logging => logging.AddProvider(Logs).AddFilter<LogsCapturados>(null, LogLevel.Trace));
    }
}

public sealed class LogsCapturados : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _lineas = new();

    public IReadOnlyCollection<string> Lineas => _lineas;

    public ILogger CreateLogger(string categoryName) => new Logger(categoryName, _lineas);

    public void Dispose() { }

    private sealed class Logger(string categoria, ConcurrentQueue<string> lineas) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        // Mensaje formateado, valores estructurados y la excepción completa: si un secreto aparece en cualquiera, se ve.
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var valores = state is IEnumerable<KeyValuePair<string, object?>> pares
                ? string.Join(" | ", pares.Select(p => $"{p.Key}={p.Value}"))
                : "";
            lineas.Enqueue($"{logLevel} {categoria}: {formatter(state, exception)} [{valores}] {exception}");
        }
    }
}
