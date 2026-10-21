using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;
using Microsoft.EntityFrameworkCore;

namespace AppNutricion.DataAccess.DataBaseContext;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    // Usuarios y objetivos
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<BodyMetric> BodyMetrics => Set<BodyMetric>();
    public DbSet<Goal> Goals => Set<Goal>();

    // Alimentos y comidas
    public DbSet<Food> Foods => Set<Food>();
    public DbSet<MealEntry> MealEntries => Set<MealEntry>();
    public DbSet<FavoriteFood> FavoriteFoods => Set<FavoriteFood>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();

    // Rutinas y entrenamientos
    public DbSet<Exercise> Exercises => Set<Exercise>();
    public DbSet<Routine> Routines => Set<Routine>();
    public DbSet<RoutineDay> RoutineDays => Set<RoutineDay>();
    public DbSet<RoutineExercise> RoutineExercises => Set<RoutineExercise>();
    public DbSet<WorkoutLog> WorkoutLogs => Set<WorkoutLog>();
    public DbSet<WorkoutSet> WorkoutSets => Set<WorkoutSet>();

    // Asistente IA
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<AssistantSuggestion> AssistantSuggestions => Set<AssistantSuggestion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Búsqueda de alimentos por similitud (índice trigram en Foods.NormalizedName).
        modelBuilder.HasPostgresExtension("pg_trgm");
        // Una configuración por tabla en /Configurations.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(9, 2);

        // Enums como texto ('LoseFat', no 0).
        configurationBuilder.Properties<BiologicalSex>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<ActivityLevel>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<GoalType>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<MealType>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<FoodSource>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<MuscleGroup>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<ChatRole>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<SuggestionStatus>().HaveConversion<string>().HaveMaxLength(20);
    }
}
