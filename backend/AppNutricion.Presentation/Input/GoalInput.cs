namespace AppNutricion.Presentation.Input;

// GoalType como texto ("LoseFat", "Maintain", "GainMuscle"). Las validaciones son del GoalManager.
public record GoalInput(string? GoalType, decimal? WeeklyRateKg, decimal? TargetWeightKg);
