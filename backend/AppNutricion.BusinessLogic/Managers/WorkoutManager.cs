using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;

namespace AppNutricion.BusinessLogic.Managers;

public class WorkoutManager(
    IWorkoutStore workoutStore, IExerciseStore exerciseStore, IRoutineStore routineStore, IBodyMetricStore metricStore,
    IUserToday userToday, TimeProvider timeProvider) : IWorkoutManager
{
    public Task<WorkoutLog?> CreateAsync(int userId, WorkoutDraft draft) => throw new NotImplementedException();

    public Task<List<WorkoutSummary>> ListAsync(int userId, DateOnly? from, DateOnly? to) => throw new NotImplementedException();

    public Task<WorkoutLog?> GetAsync(int id, int userId) => throw new NotImplementedException();

    public Task<bool> DeleteAsync(int id, int userId) => throw new NotImplementedException();
}
