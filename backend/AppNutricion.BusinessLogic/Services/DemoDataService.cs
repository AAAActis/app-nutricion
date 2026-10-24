using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;

namespace AppNutricion.BusinessLogic.Services;

// Usuario demo para la presentación. Todo se carga con los managers (las mismas validaciones que los endpoints);
// el store de usuarios solo se lee, para encontrar al demo si ya existe. Fechas relativas al "hoy" del perfil
// (TimeProvider). Idempotente: cada paso completa solo lo que falta (perfil, días de peso, objetivo activo, días
// con comidas, días con entrenamiento, receta, favorito).
public class DemoDataService(
    IAuthManager authManager, IUserAccountStore userStore, IProfileManager profileManager, IBodyMetricManager metricManager,
    IGoalManager goalManager, IFoodManager foodManager, IMealEntryManager mealManager, IRecipeManager recipeManager,
    IRoutineManager routineManager, IWorkoutManager workoutManager, IUserToday userToday) : IDemoDataService
{
    // De demo, documentados en el README: no son un secreto.
    public const string DemoEmail = "demo@example.com";
    public const string DemoPassword = "demo-nutricion-2026";
    public const string RecipeName = "Bowl de pollo con arroz";
    public const string PresetRoutineName = "Torso/Pierna (4 días)";
    private const int Days = 14;
    private const int FoodSearchLimit = 50;

    private const string Chicken = "Pollo, asado al horno";
    private const string Rice = "Arroz, grano, blanco, pulido, hervido";
    private const string Egg = "Huevo de gallina, frito";
    private const string Bread = "Trigo, pan francés";
    private const string Banana = "Banana, pulpa, fresca";
    private const string Oats = "Avena, grano, arrollada, cruda";
    private const string Cheese = "Queso cremoso";

    public async Task<DemoSeedSummary> SeedAsync(CancellationToken cancellationToken = default)
    {
        var registered = await authManager.RegisterAsync(DemoEmail, DemoPassword, consentAccepted: true);
        var user = registered.User ?? await userStore.GetByEmailAsync(DemoEmail)
                   ?? throw new InvalidOperationException("No se pudo crear ni encontrar el usuario demo.");
        var userId = user.Id;

        if (await profileManager.GetAsync(userId) is null)
            await profileManager.SaveAsync(userId, new ProfileData(new DateOnly(1994, 5, 12), nameof(BiologicalSex.Male), 176,
                nameof(ActivityLevel.Moderate), "America/Argentina/Buenos_Aires"));
        var today = await userToday.GetAsync(userId);
        var firstDay = today.AddDays(1 - Days);

        var metrics = await SeedWeightAsync(userId, firstDay, today);
        var goalCreated = false;
        if (await goalManager.GetTargetsAsync(userId) is null)
        {
            await goalManager.CreateAsync(userId, nameof(GoalType.LoseFat), 0.5m, 74m);
            goalCreated = true;
        }

        var foods = await FoodsAsync(userId);
        var (recipe, recipeCreated) = await RecipeAsync(userId, foods);
        await foodManager.AddFavoriteAsync(foods[Banana].Id, userId);
        var meals = await SeedMealsAsync(userId, foods, recipe, firstDay, today, cancellationToken);
        var workouts = await SeedWorkoutsAsync(userId, firstDay, today);

        return new DemoSeedSummary(registered.Status == RegisterStatus.Registered, metrics, meals, workouts, goalCreated, recipeCreated);
    }

    // 14 días hasta hoy, de 82 kg bajando ~0,15 kg por día con algo de ruido: la tendencia (7 contra 7) baja.
    private async Task<int> SeedWeightAsync(int userId, DateOnly firstDay, DateOnly today)
    {
        var existing = (await metricManager.GetHistoryAsync(userId, firstDay, today)).Metrics.Select(m => m.Date).ToHashSet();
        var added = 0;
        for (var i = 0; i < Days; i++)
        {
            var day = firstDay.AddDays(i);
            if (existing.Contains(day)) continue;
            await metricManager.RecordAsync(userId, 82.0m - 0.15m * i + (i % 3 - 1) * 0.1m, day);
            added++;
        }
        return added;
    }

    private async Task<Dictionary<string, Food>> FoodsAsync(int userId)
    {
        var foods = new Dictionary<string, Food>();
        foreach (var name in new[] { Chicken, Rice, Egg, Bread, Banana, Oats, Cheese })
        {
            foods[name] = (await foodManager.SearchAsync(name, userId, FoodSearchLimit)).FirstOrDefault(f => f.Name == name)
                          ?? throw new InvalidOperationException($"Falta \"{name}\" en Foods: ¿se cargó foods_seed.json?");
        }
        return foods;
    }

    private async Task<(Recipe Recipe, bool Created)> RecipeAsync(int userId, Dictionary<string, Food> foods)
    {
        var existing = (await recipeManager.ListAsync(userId)).FirstOrDefault(r => r.Recipe.Name == RecipeName);
        if (existing is not null) return (existing.Recipe, false);
        var created = await recipeManager.CreateAsync(userId, new RecipeData(RecipeName, 2,
            [new RecipeIngredientData(foods[Chicken].Id, 300), new RecipeIngredientData(foods[Rice].Id, 360)]));
        return (created.Recipe, true);
    }

    // Días sin comidas: desayuno, almuerzo, merienda y cena. Hoy, solo desayuno y almuerzo (queda "cuánto me falta").
    // La cena de ayer es la receta.
    private async Task<int> SeedMealsAsync(int userId, Dictionary<string, Food> foods, Recipe recipe, DateOnly firstDay, DateOnly today,
        CancellationToken cancellationToken)
    {
        var added = 0;
        for (var day = firstDay; day <= today; day = day.AddDays(1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if ((await mealManager.ListAsync(userId, day)).Count > 0) continue;
            var extra = day.DayNumber % 2 * 20m;   // un poco distinto día por medio
            var entries = new List<(string Food, decimal Grams, MealType Meal)>
            {
                (Oats, 50, MealType.Breakfast), (Banana, 120, MealType.Breakfast),
                (Chicken, 150 + extra, MealType.Lunch), (Rice, 180 + extra, MealType.Lunch),
            };
            if (day < today)
            {
                entries.Add((Bread, 60, MealType.AfternoonSnack));
                entries.Add((Cheese, 30, MealType.AfternoonSnack));
                if (day != today.AddDays(-1)) entries.Add((Egg, 100, MealType.Dinner));
            }
            foreach (var (food, grams, meal) in entries)
            {
                await mealManager.CreateAsync(userId, foods[food].Id, grams, false, meal, day, null);
                added++;
            }
            if (day == today.AddDays(-1))
            {
                await mealManager.CreateFromRecipeAsync(userId, recipe.Id, 1, MealType.Dinner, day);
                added++;
            }
        }
        return added;
    }

    // 3 entrenamientos con los días 1, 2 y 3 de la rutina precargada, con las series objetivo. Si no está (no se
    // cargó exercises_seed.json), sin entrenamientos.
    private async Task<int> SeedWorkoutsAsync(int userId, DateOnly firstDay, DateOnly today)
    {
        var preset = (await routineManager.ListAsync(userId)).FirstOrDefault(r => r.UserId is null && r.Name == PresetRoutineName);
        if (preset is null || await routineManager.GetAsync(preset.Id, userId) is not { } routine) return 0;
        var done = (await workoutManager.ListAsync(userId, firstDay, today)).Select(w => w.Date).ToHashSet();
        var added = 0;
        foreach (var (dayNumber, daysAgo, minutes) in new[] { (1, 6, 55), (2, 4, 50), (3, 2, 60) })
        {
            var date = today.AddDays(-daysAgo);
            var routineDay = routine.Days.FirstOrDefault(d => d.DayNumber == dayNumber);
            if (routineDay is null || done.Contains(date)) continue;
            var sets = routineDay.Exercises.OrderBy(e => e.Order)
                .SelectMany(e => Enumerable.Range(1, e.TargetSets)
                    .Select(n => new WorkoutSetDraft(e.ExerciseId, n, e.TargetReps, e.TargetWeightKg ?? 0)))
                .ToList();
            await workoutManager.CreateAsync(userId, new WorkoutDraft(routineDay.Id, date, minutes, $"Demo: {routineDay.Name}", sets));
            added++;
        }
        return added;
    }
}
