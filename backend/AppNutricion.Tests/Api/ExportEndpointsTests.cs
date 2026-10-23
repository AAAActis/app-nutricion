using System.Net;
using System.Text.Json;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;
using AppNutricion.Tests.Infraestructura;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using static AppNutricion.Tests.Infraestructura.UsuarioDePrueba;

namespace AppNutricion.Tests.Api;

// GET /api/me/export con JwtBearer real. El store es un mock: lo que se prueba es qué sale (y qué no) en el JSON.
public class ExportEndpointsTests : IClassFixture<ApiFactoryAuth>
{
    private const string HashSecreto = "HASH-QUE-NUNCA-TIENE-QUE-SALIR";
    private static readonly DateTime Base = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Dia = new(2026, 10, 20);

    private readonly Mock<IExportStore> _store = new();
    private readonly WebApplicationFactory<Program> _cliente;

    public ExportEndpointsTests(ApiFactoryAuth factory)
    {
        _cliente = factory.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
            services.Replace(ServiceDescriptor.Scoped(_ => _store.Object))));
    }

    private static UserDataExport Datos(int userId)
    {
        var sentadilla = new Exercise { Id = 7, Name = "Sentadilla", MuscleGroup = MuscleGroup.Legs, MetValue = 5 };
        var torta = new Food { Id = 9, Name = "Torta", NormalizedName = "torta", Source = FoodSource.Custom, CreatedByUserId = userId, CaloriesPer100g = 300, CreatedAt = Base };
        return new UserDataExport(
            new User { Id = userId, Email = "ana@mail.com", PasswordHash = HashSecreto, ConsentVersion = "2026-10-21", ConsentAcceptedAt = Base, CreatedAt = Base },
            new Profile { UserId = userId, BirthDate = new DateOnly(1990, 1, 1), BiologicalSex = BiologicalSex.Female, HeightCm = 165, ActivityLevel = ActivityLevel.Light, TimeZoneId = "UTC", UpdatedAt = Base },
            [new BodyMetric { UserId = userId, Date = Dia, WeightKg = 61.5m, CreatedAt = Base }],
            [new Goal { Id = 3, UserId = userId, GoalType = GoalType.Maintain, TargetCalories = 2000, IsActive = true, CreatedAt = Base }],
            [new MealEntry { UserId = userId, ItemName = "Arroz", Date = Dia, MealType = MealType.Lunch, QuantityGrams = 100, SavedCalories = 130, CreatedAt = Base }],
            [new FavoriteFood { UserId = userId, FoodId = torta.Id, Food = torta, CreatedAt = Base }],
            [torta],
            [new Recipe { UserId = userId, Name = "Mi receta", Servings = 2, CreatedAt = Base, Ingredients = [new RecipeIngredient { FoodId = torta.Id, Food = torta, QuantityGrams = 100 }] }],
            [new Routine { UserId = userId, Name = "Mi rutina", CreatedAt = Base, Days = [new RoutineDay { DayNumber = 1, Name = "Piernas", Exercises = [new RoutineExercise { ExerciseId = 7, Exercise = sentadilla, Order = 1, TargetSets = 3, TargetReps = 10 }] }] }],
            [new WorkoutLog { UserId = userId, Date = Dia, DurationMinutes = 45, CreatedAt = Base, Sets = [new WorkoutSet { ExerciseId = 7, Exercise = sentadilla, SetNumber = 1, Reps = 10, WeightKg = 40 }] }],
            [new Conversation { UserId = userId, Title = "Charla", CreatedAt = Base, UpdatedAt = Base, Messages = [new ChatMessage { Role = ChatRole.User, Content = "hola", CreatedAt = Base }] }],
            []);
    }

    [Fact]
    public async Task Export_DevuelveTodasLasSeccionesDelUsuarioDelToken()
    {
        var (id, client) = Nuevo(_cliente);
        _store.Setup(s => s.GetAsync(id)).ReturnsAsync(Datos(id));

        var response = await client.GetAsync("/api/me/export");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await JsonAsync(response);
        Assert.Equal("ana@mail.com", json.GetProperty("account").GetProperty("email").GetString());
        Assert.Equal("2026-10-21", json.GetProperty("account").GetProperty("consentVersion").GetString());
        Assert.Equal("Female", json.GetProperty("profile").GetProperty("biologicalSex").GetString());
        Assert.Equal(61.5m, json.GetProperty("bodyMetrics")[0].GetProperty("weightKg").GetDecimal());
        Assert.Equal("Maintain", json.GetProperty("goals")[0].GetProperty("goalType").GetString());
        Assert.Equal("Arroz", json.GetProperty("mealEntries")[0].GetProperty("itemName").GetString());
        Assert.Equal("Torta", json.GetProperty("favoriteFoods")[0].GetProperty("foodName").GetString());
        Assert.Equal(300m, json.GetProperty("customFoods")[0].GetProperty("caloriesPer100g").GetDecimal());
        Assert.Equal("Torta", json.GetProperty("recipes")[0].GetProperty("ingredients")[0].GetProperty("foodName").GetString());
        Assert.Equal("Sentadilla", json.GetProperty("routines")[0].GetProperty("days")[0].GetProperty("exercises")[0].GetProperty("exerciseName").GetString());
        Assert.Equal(40m, json.GetProperty("workoutLogs")[0].GetProperty("sets")[0].GetProperty("weightKg").GetDecimal());
        Assert.Equal("hola", json.GetProperty("conversations")[0].GetProperty("messages")[0].GetProperty("content").GetString());
    }

    // Nunca el hash ni nada de refresh tokens, aunque el User del store traiga el PasswordHash.
    [Fact]
    public async Task Export_NuncaIncluyeElHashNiRefreshTokens()
    {
        var (id, client) = Nuevo(_cliente);
        _store.Setup(s => s.GetAsync(id)).ReturnsAsync(Datos(id));

        var response = await client.GetAsync("/api/me/export");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cuerpo = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(HashSecreto, cuerpo);
        Assert.DoesNotContain("hash", cuerpo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", cuerpo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refresh", cuerpo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Export_SinToken_401SinLlegarAlStore()
    {
        var response = await _cliente.CreateClient().GetAsync("/api/me/export");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(_store.Invocations);
    }
}
