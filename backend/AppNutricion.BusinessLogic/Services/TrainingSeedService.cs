using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.DataAccess.Interfaces;

namespace AppNutricion.BusinessLogic.Services;

public class TrainingSeedService(IExerciseStore exerciseStore, IRoutineStore routineStore) : ITrainingSeedService
{
    public Task<TrainingSeedSummary> SeedAsync(string json) => throw new NotImplementedException();
}
