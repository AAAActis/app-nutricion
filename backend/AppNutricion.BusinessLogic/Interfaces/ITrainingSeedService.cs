namespace AppNutricion.BusinessLogic.Interfaces;

public record TrainingSeedSummary(int ExercisesInserted, int RoutinesInserted);

// Carga exercises_seed.json: ejercicios por Name y rutinas precargadas por Name. Lo que ya existe no se toca.
public interface ITrainingSeedService
{
    Task<TrainingSeedSummary> SeedAsync(string json);
}
