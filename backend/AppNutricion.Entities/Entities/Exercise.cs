using AppNutricion.Entities.Enums;

namespace AppNutricion.Entities.Entities;

// Catálogo compartido de ejercicios.
public class Exercise
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public MuscleGroup MuscleGroup { get; set; }
    public decimal MetValue { get; set; }                   // para estimar kcal quemadas (MET × kg × horas)
}
