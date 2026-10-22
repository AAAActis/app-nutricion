using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.Entities.Entities;

namespace AppNutricion.Presentation.Output;

public record MetricOutput(DateOnly Date, decimal WeightKg)
{
    public static MetricOutput From(BodyMetric m) => new(m.Date, m.WeightKg);
}

public record WeightTrendOutput(decimal LastAverageKg, decimal PreviousAverageKg, decimal ChangeKg);

// Trend null = menos de 14 registros.
public record MetricsHistoryOutput(List<MetricOutput> Metrics, WeightTrendOutput? Trend)
{
    public static MetricsHistoryOutput From(MetricsHistory h) => new(
        h.Metrics.Select(MetricOutput.From).ToList(),
        h.Trend is { } t ? new WeightTrendOutput(t.LastAverageKg, t.PreviousAverageKg, t.ChangeKg) : null);
}
