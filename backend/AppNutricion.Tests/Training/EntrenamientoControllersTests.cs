using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AppNutricion.BusinessLogic.Exceptions;
using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;
using AppNutricion.Tests.Infraestructura;
using Moq;

namespace AppNutricion.Tests.Training;

public class EntrenamientoControllersTests : IClassFixture<ApiFactoryRutinas>
{
    private const int UserId = 7;
    private static readonly DateOnly Dia = new(2026, 10, 20);

    private readonly ApiFactoryRutinas _factory;
    private readonly Exercise _sentadilla = new() { Id = 1, Name = "Sentadilla", MuscleGroup = MuscleGroup.Legs, MetValue = 5.0m };

    public EntrenamientoControllersTests(ApiFactoryRutinas factory)
    {
        _factory = factory;
        _factory.RoutineManager.Reset();
        _factory.WorkoutManager.Reset();
    }

    private static object AltaRutina() => new
    {
        name = "Mía", description = "d",
        days = new[] { new { dayNumber = 1, name = "A", exercises = new[] { new { exerciseId = 1, order = 1, targetSets = 3, targetReps = 10, targetWeightKg = (decimal?)null } } } },
    };

    private static object AltaEntrenamiento() => new
    {
        routineDayId = (int?)9, date = "2026-10-20", durationMinutes = 45, notes = "ok",
        sets = new[] { new { exerciseId = 1, setNumber = 1, reps = 8, weightKg = 100m } },
    };

    public static TheoryData<string, string> Endpoints => new()
    {
        { "GET", "/api/exercises" },
        { "GET", "/api/routines" },
        { "GET", "/api/routines/3" },
        { "POST", "/api/routines" },
        { "DELETE", "/api/routines/3" },
        { "POST", "/api/workouts" },
        { "GET", "/api/workouts?from=2026-10-01&to=2026-10-20" },
        { "GET", "/api/workouts/4" },
        { "DELETE", "/api/workouts/4" },
    };

    private static HttpRequestMessage Pedido(string metodo, string ruta) => new(new HttpMethod(metodo), ruta)
    {
        Content = metodo != "POST" ? null : JsonContent.Create(ruta.Contains("routines") ? AltaRutina() : AltaEntrenamiento()),
    };

    private Routine Rutina(int? userId) => new()
    {
        Id = 3, UserId = userId, Name = "Torso/Pierna (4 días)", Description = "4 días",
        Days =
        [
            new RoutineDay
            {
                Id = 11, DayNumber = 1, Name = "Torso A",
                Exercises = [new RoutineExercise { Id = 21, ExerciseId = 1, Exercise = _sentadilla, Order = 1, TargetSets = 4, TargetReps = 6, TargetWeightKg = 100m }],
            },
        ],
    };

    private WorkoutLog Entrenamiento() => new()
    {
        Id = 4, UserId = UserId, RoutineDayId = 9, Date = Dia, DurationMinutes = 45, EstimatedCaloriesBurned = 255, Notes = "ok",
        CreatedAt = new DateTime(2026, 10, 20, 15, 0, 0, DateTimeKind.Utc),
        Sets = [new WorkoutSet { Id = 31, ExerciseId = 1, Exercise = _sentadilla, SetNumber = 1, Reps = 8, WeightKg = 100m }],
    };

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    private static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal((int)status, (await Json(response)).GetProperty("status").GetInt32());
    }

    // ---- Autorización ----

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task SinHeader_Devuelve401(string metodo, string ruta)
    {
        var response = await _factory.CrearCliente(null).SendAsync(Pedido(metodo, ruta));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(TestAuthHandler.Esquema, response.Headers.WwwAuthenticate.ToString());
        Assert.Empty(_factory.RoutineManager.Invocations);
        Assert.Empty(_factory.WorkoutManager.Invocations);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task ConNameIdentifierYSinSub_NoLlegaAlManager(string metodo, string ruta)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderNameIdentifier, UserId.ToString());

        var response = await client.SendAsync(Pedido(metodo, ruta));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Empty(_factory.RoutineManager.Invocations);
        Assert.Empty(_factory.WorkoutManager.Invocations);
    }

    // ---- Ejercicios ----

    [Fact]
    public async Task Ejercicios_PasaElGrupoYDevuelveElCatalogo()
    {
        _factory.RoutineManager.Setup(m => m.ListExercisesAsync("legs")).ReturnsAsync([_sentadilla]);

        var response = await _factory.CrearCliente(UserId).GetAsync("/api/exercises?muscleGroup=legs");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var item = Assert.Single((await Json(response)).EnumerateArray());
        Assert.Equal(1, item.GetProperty("id").GetInt32());
        Assert.Equal("Sentadilla", item.GetProperty("name").GetString());
        Assert.Equal("Legs", item.GetProperty("muscleGroup").GetString());
        Assert.Equal(5.0m, item.GetProperty("metValue").GetDecimal());
    }

    [Fact]
    public async Task Ejercicios_GrupoInvalido_400ConElCampo()
    {
        _factory.RoutineManager.Setup(m => m.ListExercisesAsync("x")).ThrowsAsync(new ArgumentException("Grupo inválido.", "muscleGroup"));

        var response = await _factory.CrearCliente(UserId).GetAsync("/api/exercises?muscleGroup=x");

        await AssertProblem(response, HttpStatusCode.BadRequest);
        Assert.Equal("Grupo inválido.", (await Json(response)).GetProperty("errors").GetProperty("muscleGroup")[0].GetString());
    }

    // ---- Rutinas ----

    [Fact]
    public async Task Rutinas_Lista_ConIsPreset()
    {
        _factory.RoutineManager.Setup(m => m.ListAsync(UserId)).ReturnsAsync([Rutina(null), Rutina(UserId)]);

        var response = await _factory.CrearCliente(UserId).GetAsync("/api/routines");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = (await Json(response)).EnumerateArray().ToList();
        Assert.Equal([true, false], items.Select(i => i.GetProperty("isPreset").GetBoolean()));
        Assert.Equal("Torso/Pierna (4 días)", items[0].GetProperty("name").GetString());
        Assert.Equal("4 días", items[0].GetProperty("description").GetString());
        Assert.Equal(3, items[0].GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task Rutinas_Detalle_ConDiasYEjercicios()
    {
        _factory.RoutineManager.Setup(m => m.GetAsync(3, UserId)).ReturnsAsync(Rutina(null));

        var response = await _factory.CrearCliente(UserId).GetAsync("/api/routines/3");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await Json(response);
        Assert.True(json.GetProperty("isPreset").GetBoolean());
        var dia = Assert.Single(json.GetProperty("days").EnumerateArray());
        Assert.Equal((11, 1, "Torso A"), (dia.GetProperty("id").GetInt32(), dia.GetProperty("dayNumber").GetInt32(), dia.GetProperty("name").GetString()));
        var ejercicio = Assert.Single(dia.GetProperty("exercises").EnumerateArray());
        Assert.Equal(1, ejercicio.GetProperty("exerciseId").GetInt32());
        Assert.Equal("Sentadilla", ejercicio.GetProperty("exerciseName").GetString());
        Assert.Equal("Legs", ejercicio.GetProperty("muscleGroup").GetString());
        Assert.Equal((1, 4, 6), (ejercicio.GetProperty("order").GetInt32(), ejercicio.GetProperty("targetSets").GetInt32(), ejercicio.GetProperty("targetReps").GetInt32()));
        Assert.Equal(100m, ejercicio.GetProperty("targetWeightKg").GetDecimal());
    }

    [Fact]
    public async Task Rutinas_DetalleDeOtroOInexistente_404Problem()
    {
        await AssertProblem(await _factory.CrearCliente(UserId).GetAsync("/api/routines/3"), HttpStatusCode.NotFound);
        _factory.RoutineManager.Verify(m => m.GetAsync(3, UserId), Times.Once);
    }

    [Fact]
    public async Task Rutinas_Alta_201ConElDetalle_YElDraftDelBody()
    {
        RoutineDraft? recibido = null;
        _factory.RoutineManager.Setup(m => m.CreateAsync(UserId, It.IsAny<RoutineDraft>()))
            .Callback((int _, RoutineDraft d) => recibido = d)
            .ReturnsAsync(Rutina(UserId));

        var response = await _factory.CrearCliente(UserId).PostAsJsonAsync("/api/routines", AltaRutina());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.False((await Json(response)).GetProperty("isPreset").GetBoolean());
        Assert.NotNull(recibido);
        Assert.Equal(("Mía", "d"), (recibido.Name, recibido.Description));
        var dia = Assert.Single(recibido.Days!);
        Assert.Equal((1, "A"), (dia.DayNumber, dia.Name));
        Assert.Equal(new RoutineExerciseDraft(1, 1, 3, 10, null), Assert.Single(dia.Exercises!));
    }

    [Fact]
    public async Task Rutinas_AltaInvalida_400ConElCampo()
    {
        _factory.RoutineManager.Setup(m => m.CreateAsync(UserId, It.IsAny<RoutineDraft>()))
            .ThrowsAsync(new ArgumentException("El ejercicio no existe.", "days[0].exercises[0].exerciseId"));

        var response = await _factory.CrearCliente(UserId).PostAsJsonAsync("/api/routines", AltaRutina());

        await AssertProblem(response, HttpStatusCode.BadRequest);
        Assert.Equal("El ejercicio no existe.",
            (await Json(response)).GetProperty("errors").GetProperty("days[0].exercises[0].exerciseId")[0].GetString());
    }

    [Fact]
    public async Task Rutinas_Baja_204_404Y409()
    {
        _factory.RoutineManager.Setup(m => m.DeleteAsync(3, UserId)).ReturnsAsync(true);
        _factory.RoutineManager.Setup(m => m.DeleteAsync(5, UserId)).ThrowsAsync(new ConflictException("Las rutinas precargadas no se pueden borrar"));
        var client = _factory.CrearCliente(UserId);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/routines/3")).StatusCode);
        await AssertProblem(await client.DeleteAsync("/api/routines/4"), HttpStatusCode.NotFound);
        var conflicto = await client.DeleteAsync("/api/routines/5");
        await AssertProblem(conflicto, HttpStatusCode.Conflict);
        Assert.Equal("Las rutinas precargadas no se pueden borrar", (await Json(conflicto)).GetProperty("title").GetString());
    }

    // ---- Entrenamientos ----

    [Fact]
    public async Task Entrenamientos_Alta_201ConSetsKcalYDisclaimer()
    {
        WorkoutDraft? recibido = null;
        _factory.WorkoutManager.Setup(m => m.CreateAsync(UserId, It.IsAny<WorkoutDraft>()))
            .Callback((int _, WorkoutDraft d) => recibido = d)
            .ReturnsAsync(Entrenamiento());

        var response = await _factory.CrearCliente(UserId).PostAsJsonAsync("/api/workouts", AltaEntrenamiento());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await Json(response);
        Assert.Equal(4, json.GetProperty("id").GetInt32());
        Assert.Equal(9, json.GetProperty("routineDayId").GetInt32());
        Assert.Equal("2026-10-20", json.GetProperty("date").GetString());
        Assert.Equal(45, json.GetProperty("durationMinutes").GetInt32());
        Assert.Equal(255m, json.GetProperty("estimatedCaloriesBurned").GetDecimal());
        Assert.Equal("ok", json.GetProperty("notes").GetString());
        Assert.Equal("Estimación, no es consejo médico.", json.GetProperty("disclaimer").GetString());
        var set = Assert.Single(json.GetProperty("sets").EnumerateArray());
        Assert.Equal("Sentadilla", set.GetProperty("exerciseName").GetString());
        Assert.Equal((1, 1, 8, 100m), (set.GetProperty("exerciseId").GetInt32(), set.GetProperty("setNumber").GetInt32(),
            set.GetProperty("reps").GetInt32(), set.GetProperty("weightKg").GetDecimal()));
        Assert.Equal(new WorkoutDraft(9, Dia, 45, "ok", recibido!.Sets), recibido);
        Assert.Equal(new WorkoutSetDraft(1, 1, 8, 100m), Assert.Single(recibido.Sets!));
    }

    [Fact]
    public async Task Entrenamientos_Alta_404_400Y409()
    {
        var client = _factory.CrearCliente(UserId);
        _factory.WorkoutManager.Setup(m => m.CreateAsync(UserId, It.IsAny<WorkoutDraft>())).ReturnsAsync((WorkoutLog?)null);
        await AssertProblem(await client.PostAsJsonAsync("/api/workouts", AltaEntrenamiento()), HttpStatusCode.NotFound);

        _factory.WorkoutManager.Setup(m => m.CreateAsync(UserId, It.IsAny<WorkoutDraft>()))
            .ThrowsAsync(new ArgumentException("Las repeticiones van de 1 a 100.", "sets[0].reps"));
        var invalido = await client.PostAsJsonAsync("/api/workouts", AltaEntrenamiento());
        await AssertProblem(invalido, HttpStatusCode.BadRequest);
        Assert.True((await Json(invalido)).GetProperty("errors").TryGetProperty("sets[0].reps", out _));

        _factory.WorkoutManager.Setup(m => m.CreateAsync(UserId, It.IsAny<WorkoutDraft>()))
            .ThrowsAsync(new ConflictException("Primero registrá tu peso."));
        var sinPeso = await client.PostAsJsonAsync("/api/workouts", AltaEntrenamiento());
        await AssertProblem(sinPeso, HttpStatusCode.Conflict);
        Assert.Equal("Primero registrá tu peso.", (await Json(sinPeso)).GetProperty("title").GetString());
    }

    [Fact]
    public async Task Entrenamientos_Lista_ConTotales()
    {
        _factory.WorkoutManager.Setup(m => m.ListAsync(UserId, new DateOnly(2026, 10, 1), Dia))
            .ReturnsAsync([new WorkoutSummary(4, 9, Dia, 45, 255, "ok", 6, 4815m)]);

        var response = await _factory.CrearCliente(UserId).GetAsync("/api/workouts?from=2026-10-01&to=2026-10-20");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var item = Assert.Single((await Json(response)).EnumerateArray());
        Assert.Equal(4, item.GetProperty("id").GetInt32());
        Assert.Equal(6, item.GetProperty("setCount").GetInt32());
        Assert.Equal(4815m, item.GetProperty("volumeKg").GetDecimal());
        Assert.Equal(255m, item.GetProperty("estimatedCaloriesBurned").GetDecimal());
        Assert.Equal("2026-10-20", item.GetProperty("date").GetString());
    }

    [Fact]
    public async Task Entrenamientos_ListaSinRango_400ConElCampo()
    {
        _factory.WorkoutManager.Setup(m => m.ListAsync(UserId, null, null))
            .ThrowsAsync(new ArgumentException("Falta la fecha de inicio (from=YYYY-MM-DD).", "from"));

        var response = await _factory.CrearCliente(UserId).GetAsync("/api/workouts");

        await AssertProblem(response, HttpStatusCode.BadRequest);
        Assert.True((await Json(response)).GetProperty("errors").TryGetProperty("from", out _));
    }

    [Fact]
    public async Task Entrenamientos_DetalleYBaja_ConElUserId_404SiNoEsSuyo()
    {
        _factory.WorkoutManager.Setup(m => m.GetAsync(4, UserId)).ReturnsAsync(Entrenamiento());
        _factory.WorkoutManager.Setup(m => m.DeleteAsync(4, UserId)).ReturnsAsync(true);
        var client = _factory.CrearCliente(UserId);

        var detalle = await client.GetAsync("/api/workouts/4");
        Assert.Equal(HttpStatusCode.OK, detalle.StatusCode);
        Assert.Single((await Json(detalle)).GetProperty("sets").EnumerateArray());
        await AssertProblem(await client.GetAsync("/api/workouts/5"), HttpStatusCode.NotFound);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/workouts/4")).StatusCode);
        await AssertProblem(await client.DeleteAsync("/api/workouts/5"), HttpStatusCode.NotFound);
    }
}
