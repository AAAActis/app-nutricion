using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.Entities.Entities;

namespace AppNutricion.BusinessLogic.Managers;

public class BodyMetricManager : IBodyMetricManager
{
    public Task<RecordMetricResult> RecordAsync(int userId, decimal? weightKg, DateOnly? date) =>
        Task.FromResult(new RecordMetricResult(new BodyMetric(), false));

    public Task<MetricsHistory> GetHistoryAsync(int userId, DateOnly? from, DateOnly? to) =>
        Task.FromResult(new MetricsHistory([], null));
}
