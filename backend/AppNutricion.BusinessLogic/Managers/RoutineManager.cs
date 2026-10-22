using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;

namespace AppNutricion.BusinessLogic.Managers;

public class RoutineManager(IRoutineStore routineStore, IExerciseStore exerciseStore, TimeProvider timeProvider) : IRoutineManager
{
    public Task<List<Exercise>> ListExercisesAsync(string? muscleGroup) => throw new NotImplementedException();

    public Task<List<Routine>> ListAsync(int userId) => throw new NotImplementedException();

    public Task<Routine?> GetAsync(int id, int userId) => throw new NotImplementedException();

    public Task<Routine> CreateAsync(int userId, RoutineDraft draft) => throw new NotImplementedException();

    public Task<bool> DeleteAsync(int id, int userId) => throw new NotImplementedException();
}
