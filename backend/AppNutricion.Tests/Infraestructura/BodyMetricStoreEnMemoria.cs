using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;

namespace AppNutricion.Tests.Infraestructura;

// Imita a BodyMetrics: único por (UserId, Date).
public sealed class BodyMetricStoreEnMemoria : IBodyMetricStore
{
    private readonly List<BodyMetric> _registros = [];
    private readonly Lock _lock = new();
    private int _ultimoId;

    public Task<bool> UpsertForDayAsync(BodyMetric metric)
    {
        lock (_lock)
        {
            var existente = _registros.FirstOrDefault(m => m.UserId == metric.UserId && m.Date == metric.Date);
            if (existente is not null)
            {
                existente.WeightKg = metric.WeightKg;
                metric.Id = existente.Id;
                metric.CreatedAt = existente.CreatedAt;
                return Task.FromResult(false);
            }
            metric.Id = ++_ultimoId;
            _registros.Add(Copia(metric));
            return Task.FromResult(true);
        }
    }

    public Task<List<BodyMetric>> ListAsync(int userId, DateOnly? from, DateOnly? to)
    {
        lock (_lock)
            return Task.FromResult(_registros
                .Where(m => m.UserId == userId && (from is null || m.Date >= from) && (to is null || m.Date <= to))
                .OrderBy(m => m.Date).Select(Copia).ToList());
    }

    public Task<List<BodyMetric>> GetLatestAsync(int userId, int count)
    {
        lock (_lock)
            return Task.FromResult(_registros.Where(m => m.UserId == userId)
                .OrderByDescending(m => m.Date).Take(count).Select(Copia).ToList());
    }

    private static BodyMetric Copia(BodyMetric m) => new()
    {
        Id = m.Id, UserId = m.UserId, Date = m.Date, WeightKg = m.WeightKg, CreatedAt = m.CreatedAt,
    };
}
