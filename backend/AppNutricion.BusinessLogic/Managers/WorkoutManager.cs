using AppNutricion.BusinessLogic.Exceptions;
using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;

namespace AppNutricion.BusinessLogic.Managers;

public class WorkoutManager(
    IWorkoutStore workoutStore, IExerciseStore exerciseStore, IRoutineStore routineStore, IBodyMetricStore metricStore,
    IUserToday userToday, TimeProvider timeProvider) : IWorkoutManager
{
    private const int MaxDurationMinutes = 600;
    private const int MaxSets = 100;
    private const int MaxReps = 100;
    private const decimal MaxWeightKg = 500;        // 0 = peso corporal o cardio
    private const int NotesMaxLength = 500;         // largo de WorkoutLogConfiguration
    private const int MaxListDays = 366;

    public async Task<WorkoutLog?> CreateAsync(int userId, WorkoutDraft draft)
    {
        await userToday.ValidateDiaryDateAsync(userId, draft.Date, "date");
        if (draft.DurationMinutes is < 1 or > MaxDurationMinutes)
            throw new ArgumentException($"La duración va de 1 a {MaxDurationMinutes} minutos.", "durationMinutes");
        var notes = string.IsNullOrWhiteSpace(draft.Notes) ? null : draft.Notes.Trim();
        if (notes?.Length > NotesMaxLength)
            throw new ArgumentException($"Las notas son de hasta {NotesMaxLength} caracteres.", "notes");
        if (draft.Sets is not { Count: >= 1 and <= MaxSets } sets)
            throw new ArgumentException($"El entrenamiento tiene que tener de 1 a {MaxSets} series.", "sets");
        for (var i = 0; i < sets.Count; i++)
        {
            if (sets[i].SetNumber < 1)
                throw new ArgumentException("El número de serie tiene que ser mayor a 0.", $"sets[{i}].setNumber");
            if (sets[i].Reps is < 1 or > MaxReps)
                throw new ArgumentException($"Las repeticiones van de 1 a {MaxReps}.", $"sets[{i}].reps");
            if (sets[i].WeightKg is < 0 or > MaxWeightKg)
                throw new ArgumentException($"El peso va de 0 a {MaxWeightKg} kg (0 = peso corporal o cardio).", $"sets[{i}].weightKg");
        }

        var exercises = (await exerciseStore.GetByIdsAsync(sets.Select(s => s.ExerciseId))).ToDictionary(e => e.Id);
        for (var i = 0; i < sets.Count; i++)
            if (!exercises.ContainsKey(sets[i].ExerciseId))
                throw new ArgumentException("El ejercicio no existe.", $"sets[{i}].exerciseId");

        if (draft.RoutineDayId is { } dayId && !await routineStore.IsDayVisibleAsync(dayId, userId)) return null;
        var weightKg = (await metricStore.GetLatestAsync(userId, 1)).FirstOrDefault()?.WeightKg
                       ?? throw new ConflictException(GoalManager.MissingWeightMessage);

        // D22: MET promedio de los ejercicios distintos × último peso × horas, congelado al guardar.
        var averageMet = exercises.Values.Average(e => e.MetValue);
        var workout = new WorkoutLog
        {
            UserId = userId,
            RoutineDayId = draft.RoutineDayId,
            Date = draft.Date,
            DurationMinutes = draft.DurationMinutes,
            EstimatedCaloriesBurned = Math.Round(averageMet * weightKg * draft.DurationMinutes / 60, 0, MidpointRounding.AwayFromZero),
            Notes = notes,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
            Sets = sets.Select(s => new WorkoutSet { ExerciseId = s.ExerciseId, SetNumber = s.SetNumber, Reps = s.Reps, WeightKg = s.WeightKg }).ToList(),
        };
        await workoutStore.AddAsync(workout);
        // Se relee para devolver el nombre de cada ejercicio.
        return await workoutStore.GetAsync(workout.Id, userId);
    }

    public Task<List<WorkoutSummary>> ListAsync(int userId, DateOnly? from, DateOnly? to)
    {
        if (from is not { } start) throw new ArgumentException("Falta la fecha de inicio (from=YYYY-MM-DD).", nameof(from));
        if (to is not { } end) throw new ArgumentException("Falta la fecha de fin (to=YYYY-MM-DD).", nameof(to));
        if (end < start) throw new ArgumentException("La fecha de fin no puede ser anterior a la de inicio.", nameof(to));
        if (end.DayNumber - start.DayNumber + 1 > MaxListDays)
            throw new ArgumentException($"El rango es de hasta {MaxListDays} días.", nameof(to));
        return workoutStore.ListAsync(userId, start, end);
    }

    public Task<WorkoutLog?> GetAsync(int id, int userId) => workoutStore.GetAsync(id, userId);

    public Task<bool> DeleteAsync(int id, int userId) => workoutStore.DeleteAsync(id, userId);
}
