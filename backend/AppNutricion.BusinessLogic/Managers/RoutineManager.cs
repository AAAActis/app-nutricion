using AppNutricion.BusinessLogic.Exceptions;
using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.BusinessLogic.Validators;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;

namespace AppNutricion.BusinessLogic.Managers;

public class RoutineManager(IRoutineStore routineStore, IExerciseStore exerciseStore, TimeProvider timeProvider) : IRoutineManager
{
    public const string PresetDeleteMessage = "Las rutinas precargadas no se pueden borrar";
    private const int NameMaxLength = 120;          // largos de RoutineConfiguration y RoutineDayConfiguration
    private const int DescriptionMaxLength = 500;
    private const int DayNameMaxLength = 60;
    private const int MaxDays = 7;
    private const int MaxExercisesPerDay = 15;
    private const int MaxSets = 10;
    private const int MaxReps = 100;
    private const decimal MaxWeightKg = 500;

    public Task<List<Exercise>> ListExercisesAsync(string? muscleGroup)
    {
        MuscleGroup? group = null;
        if (muscleGroup is not null)
        {
            if (!EnumInput.TryParse<MuscleGroup>(muscleGroup, out var parsed))
                throw new ArgumentException($"El grupo muscular tiene que ser uno de: {string.Join(", ", Enum.GetNames<MuscleGroup>())}.", nameof(muscleGroup));
            group = parsed;
        }
        return exerciseStore.ListAsync(group);
    }

    public Task<List<Routine>> ListAsync(int userId) => routineStore.ListVisibleAsync(userId);

    public Task<Routine?> GetAsync(int id, int userId) => routineStore.GetVisibleAsync(id, userId);

    public async Task<Routine> CreateAsync(int userId, RoutineDraft draft)
    {
        var name = draft.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > NameMaxLength)
            throw new ArgumentException($"El nombre es obligatorio y de hasta {NameMaxLength} caracteres.", "name");
        var description = string.IsNullOrWhiteSpace(draft.Description) ? null : draft.Description.Trim();
        if (description?.Length > DescriptionMaxLength)
            throw new ArgumentException($"La descripción es de hasta {DescriptionMaxLength} caracteres.", "description");
        if (draft.Days is not { Count: >= 1 and <= MaxDays } days)
            throw new ArgumentException($"La rutina tiene que tener de 1 a {MaxDays} días.", "days");

        var dayNumbers = new HashSet<int>();
        for (var d = 0; d < days.Count; d++)
        {
            var day = days[d];
            if (day.DayNumber < 1 || !dayNumbers.Add(day.DayNumber))
                throw new ArgumentException("El número de día tiene que ser mayor a 0 y no repetirse.", $"days[{d}].dayNumber");
            if (string.IsNullOrWhiteSpace(day.Name) || day.Name.Trim().Length > DayNameMaxLength)
                throw new ArgumentException($"El nombre del día es obligatorio y de hasta {DayNameMaxLength} caracteres.", $"days[{d}].name");
            if (day.Exercises is not { Count: >= 1 and <= MaxExercisesPerDay } exercises)
                throw new ArgumentException($"Cada día tiene que tener de 1 a {MaxExercisesPerDay} ejercicios.", $"days[{d}].exercises");
            for (var e = 0; e < exercises.Count; e++)
            {
                var field = $"days[{d}].exercises[{e}]";
                if (exercises[e].TargetSets is < 1 or > MaxSets)
                    throw new ArgumentException($"Las series van de 1 a {MaxSets}.", $"{field}.targetSets");
                if (exercises[e].TargetReps is < 1 or > MaxReps)
                    throw new ArgumentException($"Las repeticiones van de 1 a {MaxReps}.", $"{field}.targetReps");
                if (exercises[e].TargetWeightKg is < 0 or > MaxWeightKg)
                    throw new ArgumentException($"El peso objetivo va de 0 a {MaxWeightKg} kg.", $"{field}.targetWeightKg");
            }
        }

        var existing = (await exerciseStore.GetByIdsAsync(days.SelectMany(d => d.Exercises!).Select(e => e.ExerciseId)))
            .Select(e => e.Id).ToHashSet();
        for (var d = 0; d < days.Count; d++)
            for (var e = 0; e < days[d].Exercises!.Count; e++)
                if (!existing.Contains(days[d].Exercises![e].ExerciseId))
                    throw new ArgumentException("El ejercicio no existe.", $"days[{d}].exercises[{e}].exerciseId");

        var routine = new Routine
        {
            UserId = userId,
            Name = name,
            Description = description,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
            Days = days.Select(d => new RoutineDay
            {
                DayNumber = d.DayNumber,
                Name = d.Name!.Trim(),
                Exercises = d.Exercises!.Select(e => new RoutineExercise
                {
                    ExerciseId = e.ExerciseId,
                    Order = e.Order,
                    TargetSets = e.TargetSets,
                    TargetReps = e.TargetReps,
                    TargetWeightKg = e.TargetWeightKg,
                }).ToList(),
            }).ToList(),
        };
        await routineStore.AddAsync(routine);
        // Se relee para devolver los nombres y grupos de los ejercicios.
        return (await routineStore.GetVisibleAsync(routine.Id, userId))!;
    }

    public async Task<bool> DeleteAsync(int id, int userId)
    {
        var routine = await routineStore.GetVisibleAsync(id, userId);
        if (routine is null) return false;
        if (routine.UserId is null) throw new ConflictException(PresetDeleteMessage);
        return await routineStore.DeleteOwnAsync(id, userId);
    }
}
