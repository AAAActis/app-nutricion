using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;

namespace AppNutricion.DataAccess.Interfaces;

// Catálogo compartido: no filtra por usuario.
public interface IExerciseStore
{
    // Ordenados por nombre. muscleGroup null = todos.
    Task<List<Exercise>> ListAsync(MuscleGroup? muscleGroup);

    // Los que existen de esos ids (los que no existen no vienen).
    Task<List<Exercise>> GetByIdsAsync(IEnumerable<int> ids);

    // Seed: inserta los que no existen por Name (único). Devuelve cuántos insertó.
    Task<int> AddMissingAsync(IEnumerable<Exercise> exercises);
}
