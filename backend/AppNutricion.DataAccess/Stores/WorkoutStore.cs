using AppNutricion.DataAccess.DataBaseContext;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;

namespace AppNutricion.DataAccess.Stores;

public class WorkoutStore(ApplicationDbContext context) : IWorkoutStore
{
    public Task AddAsync(WorkoutLog workout) => throw new NotImplementedException();

    public Task<WorkoutLog?> GetAsync(int id, int userId) => throw new NotImplementedException();

    public Task<List<WorkoutSummary>> ListAsync(int userId, DateOnly from, DateOnly to) => throw new NotImplementedException();

    public Task<bool> DeleteAsync(int id, int userId) => throw new NotImplementedException();
}
