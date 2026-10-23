using AppNutricion.DataAccess.DataBaseContext;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using Microsoft.EntityFrameworkCore;

namespace AppNutricion.DataAccess.Stores;

public class WorkoutStore(ApplicationDbContext context) : IWorkoutStore
{
    public async Task AddAsync(WorkoutLog workout)
    {
        context.WorkoutLogs.Add(workout);
        await context.SaveChangesAsync();
    }

    public Task<WorkoutLog?> GetAsync(int id, int userId) =>
        OfUser(userId)
            .Include(w => w.Sets.OrderBy(s => s.Id))
            .ThenInclude(s => s.Exercise)
            .FirstOrDefaultAsync(w => w.Id == id);

    public Task<List<WorkoutSummary>> ListAsync(int userId, DateOnly from, DateOnly to) =>
        OfUser(userId).Where(w => w.Date >= from && w.Date <= to)
            .OrderByDescending(w => w.Date).ThenByDescending(w => w.CreatedAt).ThenByDescending(w => w.Id)
            .Select(w => new WorkoutSummary(
                w.Id, w.RoutineDayId, w.Date, w.DurationMinutes, w.EstimatedCaloriesBurned, w.Notes,
                w.Sets.Count, w.Sets.Sum(s => s.Reps * s.WeightKg)))
            .ToListAsync();

    public async Task<bool> DeleteAsync(int id, int userId) =>
        await OfUser(userId).Where(w => w.Id == id).ExecuteDeleteAsync() > 0;

    private IQueryable<WorkoutLog> OfUser(int userId) =>
        context.WorkoutLogs.AsNoTracking().Where(w => w.UserId == userId);
}
