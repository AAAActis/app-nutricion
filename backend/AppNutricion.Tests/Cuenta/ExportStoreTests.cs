using AppNutricion.DataAccess.Stores;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;
using AppNutricion.Tests.Infraestructura;

namespace AppNutricion.Tests.Cuenta;

// Cada dato existe para Yo y para Otro: el export de Yo trae solo lo suyo, con sus hijos cargados.
public sealed class ExportStoreTests : IDisposable
{
    private const int Yo = 1;
    private const int Otro = 2;
    private const int NoExiste = 3;
    private static readonly DateTime Base = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Dia = new(2026, 10, 20);

    private readonly BaseSqlite _db = new(Yo, Otro);

    public ExportStoreTests()
    {
        using var context = _db.NuevoContexto();
        var sentadilla = new Exercise { Name = "Sentadilla", MuscleGroup = MuscleGroup.Legs, MetValue = 5 };
        var arroz = Alimento("Arroz", null);
        context.Exercises.Add(sentadilla);
        context.Foods.Add(arroz);
        context.SaveChanges();
        context.Routines.Add(Rutina(null, "Precargada", sentadilla));
        context.SaveChanges();

        foreach (var userId in new[] { Yo, Otro })
        {
            var propio = Alimento($"Torta de {userId}", userId);
            context.Foods.Add(propio);
            context.SaveChanges();
            context.Profiles.Add(new Profile
            {
                UserId = userId, BirthDate = new DateOnly(1990, 1, 1), BiologicalSex = BiologicalSex.Female, HeightCm = 165,
                ActivityLevel = ActivityLevel.Moderate, TimeZoneId = "UTC", UpdatedAt = Base,
            });
            context.BodyMetrics.Add(new BodyMetric { UserId = userId, Date = Dia, WeightKg = 60 + userId, CreatedAt = Base });
            context.Goals.Add(new Goal { UserId = userId, GoalType = GoalType.Maintain, TargetCalories = 2000, IsActive = true, CreatedAt = Base });
            context.MealEntries.Add(new MealEntry
            {
                UserId = userId, FoodId = arroz.Id, ItemName = $"Arroz de {userId}", Date = Dia, MealType = MealType.Lunch,
                QuantityGrams = 100, SavedCalories = 130, CreatedAt = Base,
            });
            context.FavoriteFoods.Add(new FavoriteFood { UserId = userId, FoodId = arroz.Id, CreatedAt = Base });
            context.Recipes.Add(new Recipe
            {
                UserId = userId, Name = $"Receta de {userId}", Servings = 2, CreatedAt = Base,
                Ingredients = [new RecipeIngredient { FoodId = propio.Id, QuantityGrams = 100 }],
            });
            context.Routines.Add(Rutina(userId, $"Rutina de {userId}", sentadilla));
            context.WorkoutLogs.Add(new WorkoutLog
            {
                UserId = userId, Date = Dia, DurationMinutes = 45, CreatedAt = Base,
                Sets = [new WorkoutSet { ExerciseId = sentadilla.Id, SetNumber = 1, Reps = 10, WeightKg = 40 }],
            });
            context.Conversations.Add(new Conversation
            {
                UserId = userId, Title = $"Charla de {userId}", CreatedAt = Base, UpdatedAt = Base,
                Messages = [new ChatMessage { Role = ChatRole.User, Content = $"hola de {userId}", CreatedAt = Base }],
            });
            context.SaveChanges();
        }
    }

    public void Dispose() => _db.Dispose();

    private static Food Alimento(string nombre, int? owner) => new()
    {
        Name = nombre, NormalizedName = nombre.ToLowerInvariant(), Source = owner is null ? FoodSource.Argenfoods : FoodSource.Custom,
        CreatedByUserId = owner, ExternalId = owner is null ? nombre : null, CreatedAt = Base,
    };

    private static Routine Rutina(int? userId, string nombre, Exercise ejercicio) => new()
    {
        UserId = userId, Name = nombre, CreatedAt = Base,
        Days = [new RoutineDay { DayNumber = 1, Name = "Piernas", Exercises = [new RoutineExercise { ExerciseId = ejercicio.Id, Order = 1, TargetSets = 3, TargetReps = 10 }] }],
    };

    [Fact]
    public async Task Get_TraeSoloLoDelUsuarioConSusHijos()
    {
        var e = await new ExportStore(_db.NuevoContexto()).GetAsync(Yo);

        Assert.NotNull(e);
        Assert.Equal(Yo, e.User.Id);
        Assert.Equal(Yo, e.Profile?.UserId);
        Assert.Equal([61m], e.BodyMetrics.Select(b => b.WeightKg));
        Assert.Equal([Yo], e.Goals.Select(g => g.UserId));
        Assert.Equal(["Arroz de 1"], e.MealEntries.Select(m => m.ItemName));
        Assert.Equal(["Arroz"], e.FavoriteFoods.Select(f => f.Food.Name));
        Assert.Equal(["Torta de 1"], e.CustomFoods.Select(f => f.Name));
        var receta = Assert.Single(e.Recipes);
        Assert.Equal("Torta de 1", Assert.Single(receta.Ingredients).Food.Name);
        var rutina = Assert.Single(e.Routines);   // ni la precargada ni la del otro
        Assert.Equal("Rutina de 1", rutina.Name);
        Assert.Equal("Sentadilla", Assert.Single(Assert.Single(rutina.Days).Exercises).Exercise.Name);
        Assert.Equal("Sentadilla", Assert.Single(Assert.Single(e.WorkoutLogs).Sets).Exercise.Name);
        Assert.Equal("hola de 1", Assert.Single(Assert.Single(e.Conversations).Messages).Content);
    }

    [Fact]
    public async Task Get_UsuarioQueNoExiste_Null()
    {
        Assert.Null(await new ExportStore(_db.NuevoContexto()).GetAsync(NoExiste));
    }
}
