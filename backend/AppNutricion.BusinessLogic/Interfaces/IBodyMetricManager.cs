using AppNutricion.Entities.Entities;

namespace AppNutricion.BusinessLogic.Interfaces;

public record RecordMetricResult(BodyMetric Metric, bool Created);

// Promedio de los últimos 7 registros contra el de los 7 anteriores. ChangeKg negativo = bajó.
public record WeightTrend(decimal LastAverageKg, decimal PreviousAverageKg, decimal ChangeKg);

public record MetricsHistory(List<BodyMetric> Metrics, WeightTrend? Trend);

public interface IBodyMetricManager
{
    // Sin perfil → ConflictException. Datos inválidos → ArgumentException (weightKg / date).
    // date null = hoy en la zona horaria del perfil.
    Task<RecordMetricResult> RecordAsync(int userId, decimal? weightKg, DateOnly? date);

    // Sin perfil → ConflictException. from > to → ArgumentException (from).
    Task<MetricsHistory> GetHistoryAsync(int userId, DateOnly? from, DateOnly? to);
}
