using AppNutricion.Entities.Enums;

namespace AppNutricion.Entities.Entities;

// Los targets se congelan al crearse: si cambia el peso, se crea un Goal nuevo.
public class Goal
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public GoalType GoalType { get; set; }
    public decimal? TargetWeightKg { get; set; }
    public decimal WeeklyRateKg { get; set; }
    public decimal TargetCalories { get; set; }
    public decimal TargetProteinG { get; set; }
    public decimal TargetCarbsG { get; set; }
    public decimal TargetFatG { get; set; }
    public bool IsActive { get; set; }                      // uno solo activo por usuario (índice único parcial)
    public DateTime CreatedAt { get; set; }
}
