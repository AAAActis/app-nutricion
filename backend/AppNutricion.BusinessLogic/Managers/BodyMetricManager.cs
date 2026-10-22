using AppNutricion.BusinessLogic.Constants;
using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;

namespace AppNutricion.BusinessLogic.Managers;

public class BodyMetricManager(IBodyMetricStore metricStore, IProfileStore profileStore, TimeProvider timeProvider) : IBodyMetricManager
{
    public async Task<RecordMetricResult> RecordAsync(int userId, decimal? weightKg, DateOnly? date)
    {
        var (_, today) = await ProfileGuard.RequireAsync(profileStore, userId, timeProvider);

        if (weightKg is not { } weight || weight < HealthDataLimits.MinWeightKg || weight > HealthDataLimits.MaxWeightKg)
            throw new ArgumentException($"El peso tiene que estar entre {HealthDataLimits.MinWeightKg} y {HealthDataLimits.MaxWeightKg} kg.", "weightKg");

        var day = date ?? today;
        if (day > today || day < today.AddYears(-HealthDataLimits.MaxMetricAgeYears))
            throw new ArgumentException($"La fecha no puede ser futura ni de hace más de {HealthDataLimits.MaxMetricAgeYears} años.", "date");

        var metric = new BodyMetric
        {
            UserId = userId,
            Date = day,
            WeightKg = weight,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
        };
        var created = await metricStore.UpsertForDayAsync(metric);
        return new RecordMetricResult(metric, created);
    }

    public async Task<MetricsHistory> GetHistoryAsync(int userId, DateOnly? from, DateOnly? to)
    {
        await ProfileGuard.RequireAsync(profileStore, userId, timeProvider);
        if (from > to)
            throw new ArgumentException("La fecha 'from' no puede ser posterior a 'to'.", "from");

        var metrics = await metricStore.ListAsync(userId, from, to);
        // La tendencia no depende del rango pedido: siempre los últimos 7 registros contra los 7 anteriores.
        var latest = await metricStore.GetLatestAsync(userId, 2 * HealthDataLimits.TrendWindowRecords);
        return new MetricsHistory(metrics, TrendOf(latest));
    }

    // latest viene del más nuevo al más viejo.
    private static WeightTrend? TrendOf(List<BodyMetric> latest)
    {
        if (latest.Count < 2 * HealthDataLimits.TrendWindowRecords) return null;
        var last = latest.Take(HealthDataLimits.TrendWindowRecords).Average(m => m.WeightKg);
        var previous = latest.Skip(HealthDataLimits.TrendWindowRecords).Average(m => m.WeightKg);
        return new WeightTrend(Round(last), Round(previous), Round(last - previous));
    }

    private static decimal Round(decimal kg) => Math.Round(kg, HealthDataLimits.DecimalsStored, MidpointRounding.AwayFromZero);
}
