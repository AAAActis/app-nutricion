using AppNutricion.DataAccess.DataBaseContext;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using Microsoft.EntityFrameworkCore;

namespace AppNutricion.DataAccess.Stores;

public class BodyMetricStore(ApplicationDbContext context) : IBodyMetricStore
{
    public async Task<bool> UpsertForDayAsync(BodyMetric metric)
    {
        var current = await context.BodyMetrics.FirstOrDefaultAsync(m => m.UserId == metric.UserId && m.Date == metric.Date);
        if (current is null)
        {
            context.BodyMetrics.Add(metric);
            await context.SaveChangesAsync();
            return true;
        }

        // Ya hay un registro ese día: se corrige el peso, se conserva cuándo se creó.
        current.WeightKg = metric.WeightKg;
        await context.SaveChangesAsync();
        metric.Id = current.Id;
        metric.CreatedAt = current.CreatedAt;
        return false;
    }

    public Task<List<BodyMetric>> ListAsync(int userId, DateOnly? from, DateOnly? to)
    {
        var query = context.BodyMetrics.AsNoTracking().Where(m => m.UserId == userId);
        if (from is not null) query = query.Where(m => m.Date >= from);
        if (to is not null) query = query.Where(m => m.Date <= to);
        return query.OrderBy(m => m.Date).ToListAsync();
    }

    public Task<List<BodyMetric>> GetLatestAsync(int userId, int count) =>
        context.BodyMetrics.AsNoTracking().Where(m => m.UserId == userId)
            .OrderByDescending(m => m.Date).Take(count).ToListAsync();
}
