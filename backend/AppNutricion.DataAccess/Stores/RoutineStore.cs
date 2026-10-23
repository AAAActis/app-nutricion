using AppNutricion.DataAccess.DataBaseContext;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using Microsoft.EntityFrameworkCore;

namespace AppNutricion.DataAccess.Stores;

public class RoutineStore(ApplicationDbContext context) : IRoutineStore
{
    public Task<List<Routine>> ListVisibleAsync(int userId) =>
        Visible(userId).OrderBy(r => r.UserId != null).ThenBy(r => r.Name).ThenBy(r => r.Id).ToListAsync();

    public Task<Routine?> GetVisibleAsync(int id, int userId) =>
        Visible(userId)
            .Include(r => r.Days.OrderBy(d => d.DayNumber))
            .ThenInclude(d => d.Exercises.OrderBy(e => e.Order).ThenBy(e => e.Id))
            .ThenInclude(e => e.Exercise)
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == id);

    public Task<bool> IsDayVisibleAsync(int routineDayId, int userId) =>
        context.Set<RoutineDay>().AnyAsync(d => d.Id == routineDayId && Visible(userId).Any(r => r.Id == d.RoutineId));

    public async Task AddAsync(Routine routine)
    {
        context.Routines.Add(routine);
        await context.SaveChangesAsync();
    }

    // Días y ejercicios se van en cascada; los WorkoutLogs quedan con RoutineDayId null (lo hace la FK en la base).
    public async Task<bool> DeleteOwnAsync(int id, int userId) =>
        await context.Routines.Where(r => r.Id == id && r.UserId == userId).ExecuteDeleteAsync() > 0;

    public async Task<int> AddMissingPresetsAsync(IEnumerable<Routine> routines)
    {
        var existing = (await context.Routines.Where(r => r.UserId == null).Select(r => r.Name).ToListAsync()).ToHashSet();
        var missing = routines.Where(r => existing.Add(r.Name)).ToList();
        context.Routines.AddRange(missing);
        await context.SaveChangesAsync();
        return missing.Count;
    }

    private IQueryable<Routine> Visible(int userId) =>
        context.Routines.AsNoTracking().Where(r => r.UserId == null || r.UserId == userId);
}
