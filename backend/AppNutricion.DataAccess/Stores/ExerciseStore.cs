using AppNutricion.DataAccess.DataBaseContext;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;
using Microsoft.EntityFrameworkCore;

namespace AppNutricion.DataAccess.Stores;

public class ExerciseStore(ApplicationDbContext context) : IExerciseStore
{
    public Task<List<Exercise>> ListAsync(MuscleGroup? muscleGroup)
    {
        var query = context.Exercises.AsNoTracking();
        if (muscleGroup is { } group) query = query.Where(e => e.MuscleGroup == group);
        return query.OrderBy(e => e.Name).ToListAsync();
    }

    public Task<List<Exercise>> GetByIdsAsync(IEnumerable<int> ids)
    {
        var list = ids.Distinct().ToList();
        return context.Exercises.AsNoTracking().Where(e => list.Contains(e.Id)).ToListAsync();
    }

    public async Task<int> AddMissingAsync(IEnumerable<Exercise> exercises)
    {
        var existing = (await context.Exercises.Select(e => e.Name).ToListAsync()).ToHashSet();
        var missing = exercises.Where(e => existing.Add(e.Name)).ToList();
        context.Exercises.AddRange(missing);
        await context.SaveChangesAsync();
        return missing.Count;
    }
}
