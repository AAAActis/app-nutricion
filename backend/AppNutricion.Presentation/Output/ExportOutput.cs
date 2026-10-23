using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;

namespace AppNutricion.Presentation.Output;

// GET /api/me/export (J5). Cada entidad se mapea a un DTO propio: ninguna se serializa directo.
// La cuenta sale sin PasswordHash; los RefreshTokens no se exportan.
public record ExportOutput(
    ExportAccountOutput Account,
    ProfileOutput? Profile,
    List<ExportBodyMetricOutput> BodyMetrics,
    List<GoalOutput> Goals,
    List<ExportMealEntryOutput> MealEntries,
    List<ExportFavoriteFoodOutput> FavoriteFoods,
    List<ExportFoodOutput> CustomFoods,
    List<ExportRecipeOutput> Recipes,
    List<ExportRoutineOutput> Routines,
    List<ExportWorkoutLogOutput> WorkoutLogs,
    List<ExportConversationOutput> Conversations,
    List<ExportAssistantSuggestionOutput> AssistantSuggestions)
{
    public static ExportOutput From(UserDataExport e) => new(
        ExportAccountOutput.From(e.User),
        e.Profile is null ? null : ProfileOutput.From(e.Profile),
        e.BodyMetrics.Select(b => new ExportBodyMetricOutput(b.Date, b.WeightKg, b.CreatedAt)).ToList(),
        e.Goals.Select(GoalOutput.From).ToList(),
        e.MealEntries.Select(m => new ExportMealEntryOutput(m.Date, m.MealType.ToString(), m.ItemName, m.FoodId, m.RecipeId,
            m.QuantityGrams, m.SavedCalories, m.SavedProtein, m.SavedCarbs, m.SavedFat, m.CreatedAt)).ToList(),
        e.FavoriteFoods.Select(f => new ExportFavoriteFoodOutput(f.FoodId, f.Food.Name, f.CreatedAt)).ToList(),
        e.CustomFoods.Select(ExportFoodOutput.From).ToList(),
        e.Recipes.Select(r => new ExportRecipeOutput(r.Id, r.Name, r.Servings, r.CreatedAt,
            r.Ingredients.Select(i => new ExportRecipeIngredientOutput(i.FoodId, i.Food.Name, i.QuantityGrams)).ToList())).ToList(),
        e.Routines.Select(r => new ExportRoutineOutput(r.Id, r.Name, r.Description, r.CreatedAt,
            r.Days.Select(d => new ExportRoutineDayOutput(d.DayNumber, d.Name,
                d.Exercises.Select(x => new ExportRoutineExerciseOutput(x.Order, x.ExerciseId, x.Exercise.Name, x.TargetSets, x.TargetReps, x.TargetWeightKg)).ToList())).ToList())).ToList(),
        e.WorkoutLogs.Select(w => new ExportWorkoutLogOutput(w.Date, w.RoutineDayId, w.DurationMinutes, w.EstimatedCaloriesBurned, w.Notes, w.CreatedAt,
            w.Sets.Select(s => new ExportWorkoutSetOutput(s.SetNumber, s.ExerciseId, s.Exercise.Name, s.Reps, s.WeightKg)).ToList())).ToList(),
        e.Conversations.Select(c => new ExportConversationOutput(c.Title, c.CreatedAt, c.UpdatedAt,
            c.Messages.Select(m => new ExportChatMessageOutput(m.Role.ToString(), m.Content, m.CreatedAt)).ToList())).ToList(),
        e.AssistantSuggestions.Select(s => new ExportAssistantSuggestionOutput(s.FoodId, s.Food.Name, s.QuantityGrams, s.MealType.ToString(), s.Date,
            s.Status.ToString(), s.MealEntryId, s.CreatedAt, s.ResolvedAt)).ToList());
}

public record ExportAccountOutput(int Id, string Email, DateTime ConsentAcceptedAt, string ConsentVersion, DateTime CreatedAt)
{
    public static ExportAccountOutput From(User u) => new(u.Id, u.Email, u.ConsentAcceptedAt, u.ConsentVersion, u.CreatedAt);
}

public record ExportBodyMetricOutput(DateOnly Date, decimal WeightKg, DateTime CreatedAt);

public record ExportMealEntryOutput(DateOnly Date, string MealType, string ItemName, int? FoodId, int? RecipeId, decimal QuantityGrams,
    decimal Calories, decimal ProteinG, decimal CarbsG, decimal FatG, DateTime CreatedAt);

public record ExportFavoriteFoodOutput(int FoodId, string FoodName, DateTime CreatedAt);

public record ExportFoodOutput(int Id, string Name, string? Brand, decimal CaloriesPer100g, decimal ProteinPer100g, decimal CarbsPer100g,
    decimal FatPer100g, string? PortionName, decimal? PortionWeightGrams, DateTime CreatedAt)
{
    public static ExportFoodOutput From(Food f) => new(f.Id, f.Name, f.Brand, f.CaloriesPer100g, f.ProteinPer100g, f.CarbsPer100g,
        f.FatPer100g, f.PortionName, f.PortionWeightGrams, f.CreatedAt);
}

public record ExportRecipeIngredientOutput(int FoodId, string FoodName, decimal QuantityGrams);

public record ExportRecipeOutput(int Id, string Name, int Servings, DateTime CreatedAt, List<ExportRecipeIngredientOutput> Ingredients);

public record ExportRoutineExerciseOutput(int Order, int ExerciseId, string ExerciseName, int TargetSets, int TargetReps, decimal? TargetWeightKg);

public record ExportRoutineDayOutput(int DayNumber, string Name, List<ExportRoutineExerciseOutput> Exercises);

public record ExportRoutineOutput(int Id, string Name, string? Description, DateTime CreatedAt, List<ExportRoutineDayOutput> Days);

public record ExportWorkoutSetOutput(int SetNumber, int ExerciseId, string ExerciseName, int Reps, decimal WeightKg);

public record ExportWorkoutLogOutput(DateOnly Date, int? RoutineDayId, int DurationMinutes, decimal EstimatedCaloriesBurned, string? Notes,
    DateTime CreatedAt, List<ExportWorkoutSetOutput> Sets);

public record ExportChatMessageOutput(string Role, string Content, DateTime CreatedAt);

public record ExportConversationOutput(string? Title, DateTime CreatedAt, DateTime UpdatedAt, List<ExportChatMessageOutput> Messages);

public record ExportAssistantSuggestionOutput(int FoodId, string FoodName, decimal QuantityGrams, string MealType, DateOnly Date, string Status,
    int? MealEntryId, DateTime CreatedAt, DateTime? ResolvedAt);
