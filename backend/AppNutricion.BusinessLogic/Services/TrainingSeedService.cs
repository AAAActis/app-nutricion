using System.Text.Json;
using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.BusinessLogic.Validators;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;

namespace AppNutricion.BusinessLogic.Services;

// Un archivo mal armado (grupo desconocido, ejercicio que no está en el catálogo) es un error del repo:
// se corta con InvalidOperationException y TrainingSeedHostedService lo loguea.
public class TrainingSeedService(IExerciseStore exerciseStore, IRoutineStore routineStore) : ITrainingSeedService
{
    private record SeedExercise(string Name, string? MuscleGroup, decimal MetValue);
    private record SeedRoutineExercise(string Exercise, int Order, int TargetSets, int TargetReps);
    private record SeedDay(int DayNumber, string Name, List<SeedRoutineExercise> Exercises);
    private record SeedRoutine(string Name, string? Description, List<SeedDay> Days);
    private record SeedFile(List<SeedExercise> Exercises, List<SeedRoutine> Routines);

    public async Task<TrainingSeedSummary> SeedAsync(string json)
    {
        var file = JsonSerializer.Deserialize<SeedFile>(json, JsonSerializerOptions.Web)
                   ?? throw new InvalidOperationException("exercises_seed: archivo vacío");

        var exercises = file.Exercises.Select(e => new Exercise
        {
            Name = e.Name,
            MuscleGroup = EnumInput.TryParse<MuscleGroup>(e.MuscleGroup, out var group)
                ? group
                : throw new InvalidOperationException($"exercises_seed: grupo muscular desconocido '{e.MuscleGroup}' en {e.Name}"),
            MetValue = e.MetValue,
        }).ToList();
        var exercisesInserted = await exerciseStore.AddMissingAsync(exercises);

        var ids = (await exerciseStore.ListAsync(null)).ToDictionary(e => e.Name, e => e.Id);
        var routines = file.Routines.Select(r => new Routine
        {
            UserId = null,
            Name = r.Name,
            Description = r.Description,
            CreatedAt = DateTime.UtcNow,
            Days = r.Days.Select(d => new RoutineDay
            {
                DayNumber = d.DayNumber,
                Name = d.Name,
                Exercises = d.Exercises.Select(e => new RoutineExercise
                {
                    ExerciseId = ids.TryGetValue(e.Exercise, out var id)
                        ? id
                        : throw new InvalidOperationException($"exercises_seed: la rutina {r.Name} usa '{e.Exercise}', que no está en el catálogo"),
                    Order = e.Order,
                    TargetSets = e.TargetSets,
                    TargetReps = e.TargetReps,
                }).ToList(),
            }).ToList(),
        }).ToList();
        var routinesInserted = await routineStore.AddMissingPresetsAsync(routines);

        return new TrainingSeedSummary(exercisesInserted, routinesInserted);
    }
}
