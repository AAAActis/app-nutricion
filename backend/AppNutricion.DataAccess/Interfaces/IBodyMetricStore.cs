using AppNutricion.Entities.Entities;

namespace AppNutricion.DataAccess.Interfaces;

public interface IBodyMetricStore
{
    // Un registro por usuario y día: si ya hay uno ese día se actualiza el peso (no se duplica). true = se creó.
    Task<bool> UpsertForDayAsync(BodyMetric metric);

    // Ordenados por fecha ascendente. from/to null = sin límite de ese lado.
    Task<List<BodyMetric>> ListAsync(int userId, DateOnly? from, DateOnly? to);

    // Los últimos "count" registros, del más nuevo al más viejo.
    Task<List<BodyMetric>> GetLatestAsync(int userId, int count);
}
