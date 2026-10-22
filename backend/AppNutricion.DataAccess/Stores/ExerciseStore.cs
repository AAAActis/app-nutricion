using AppNutricion.DataAccess.DataBaseContext;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;

namespace AppNutricion.DataAccess.Stores;

public class ExerciseStore(ApplicationDbContext context) : IExerciseStore
{
    public Task<List<Exercise>> ListAsync(MuscleGroup? muscleGroup) => throw new NotImplementedException();

    public Task<List<Exercise>> GetByIdsAsync(IEnumerable<int> ids) => throw new NotImplementedException();

    public Task<int> AddMissingAsync(IEnumerable<Exercise> exercises) => throw new NotImplementedException();
}
