using System.Text.Json;
using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.BusinessLogic.Services;
using AppNutricion.DataAccess.Stores;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;
using AppNutricion.Tests.Infraestructura;
using Moq;

namespace AppNutricion.Tests.Asistente;

// La IA lee las rutinas: RoutineSummaryProvider con el RoutineStore real (SQLite) y la herramienta consultar_rutinas.
// Una rutina de otro usuario nunca aparece; el userId lo pone la API.
public sealed class RutinasDelAsistenteTests : IDisposable
{
    private const int Yo = 4817;
    private const int Otro = 5923;
    private static readonly DateTime Ahora = new(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);

    private readonly BaseSqlite _db = new(Yo, Otro);
    private readonly Exercise _sentadilla = new() { Name = "Sentadilla", MuscleGroup = MuscleGroup.Legs, MetValue = 5.0m };
    private readonly Exercise _banca = new() { Name = "Press de banca", MuscleGroup = MuscleGroup.Chest, MetValue = 3.5m };
    private readonly Exercise _curl = new() { Name = "Curl de bíceps", MuscleGroup = MuscleGroup.Arms, MetValue = 3.5m };

    public RutinasDelAsistenteTests()
    {
        using var context = _db.NuevoContexto();
        context.Exercises.AddRange(_sentadilla, _banca, _curl);
        context.SaveChanges();
        context.Routines.AddRange(Rutina(null, "Full body (3 días)"), Rutina(Yo, "Mi torso/pierna"), Rutina(Otro, "Rutina secreta de otro"));
        context.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    // Días y ejercicios cargados fuera de orden, para ver que salen ordenados.
    private Routine Rutina(int? userId, string nombre) => new()
    {
        UserId = userId, Name = nombre, Description = "Descripción libre del usuario", CreatedAt = Ahora,
        Days =
        [
            new RoutineDay
            {
                DayNumber = 2, Name = "Pierna",
                Exercises = [new RoutineExercise { ExerciseId = _sentadilla.Id, Order = 1, TargetSets = 3, TargetReps = 8, TargetWeightKg = 80 }],
            },
            new RoutineDay
            {
                DayNumber = 1, Name = "Torso",
                Exercises =
                [
                    new RoutineExercise { ExerciseId = _curl.Id, Order = 2, TargetSets = 3, TargetReps = 12 },
                    new RoutineExercise { ExerciseId = _banca.Id, Order = 1, TargetSets = 4, TargetReps = 6, TargetWeightKg = 60 },
                ],
            },
        ],
    };

    private RoutineSummaryProvider Provider() => new(new RoutineStore(_db.NuevoContexto()));

    private AssistantTools Tools() => new(Mock.Of<IFoodManager>(), Mock.Of<ISummaryManager>(), Provider(), Mock.Of<IUserToday>());

    private async Task<string> ConsultarRutinas(int userId, string argumentos = "{}") =>
        (await Tools().ExecuteAsync(userId, new ChatToolCall("c1", AssistantTools.ListRoutines, argumentos))).Result;

    // ---- RoutineSummaryProvider ----

    [Fact]
    public async Task Provider_PrecargadasYPropias_NuncaLasDeOtro()
    {
        var rutinas = await Provider().GetAsync(Yo);

        Assert.Equal([("Full body (3 días)", true), ("Mi torso/pierna", false)], rutinas.Select(r => (r.Name, r.IsPreset)));
    }

    [Fact]
    public async Task Provider_ElOtroUsuarioVeLaSuyaYNoLaMia()
    {
        var rutinas = await Provider().GetAsync(Otro);

        Assert.Equal(["Full body (3 días)", "Rutina secreta de otro"], rutinas.Select(r => r.Name));
    }

    [Fact]
    public async Task Provider_DiasYEjerciciosEnOrden()
    {
        var mia = Assert.Single(await Provider().GetAsync(Yo), r => r.Name == "Mi torso/pierna");

        Assert.Equal([(1, "Torso"), (2, "Pierna")], mia.Days.Select(d => (d.DayNumber, d.Name)));
        Assert.Equal(
            [new RoutineExerciseSummary("Press de banca", "Chest", 4, 6, 60), new RoutineExerciseSummary("Curl de bíceps", "Arms", 3, 12, null)],
            mia.Days[0].Exercises);
        Assert.Equal([new RoutineExerciseSummary("Sentadilla", "Legs", 3, 8, 80)], mia.Days[1].Exercises);
    }

    // ---- consultar_rutinas ----

    [Fact]
    public async Task ConsultarRutinas_RutinasRealesConDiasYEjercicios()
    {
        var json = JsonDocument.Parse(await ConsultarRutinas(Yo)).RootElement;

        var rutinas = json.GetProperty("rutinas");
        Assert.Equal(["Full body (3 días)", "Mi torso/pierna"], rutinas.EnumerateArray().Select(r => r.GetProperty("nombre").GetString()));
        var mia = rutinas[1];
        Assert.True(rutinas[0].GetProperty("precargada").GetBoolean());
        Assert.False(mia.GetProperty("precargada").GetBoolean());
        var dias = mia.GetProperty("dias");
        Assert.Equal([(1, "Torso"), (2, "Pierna")], dias.EnumerateArray().Select(d => (d.GetProperty("numero").GetInt32(), d.GetProperty("nombre").GetString())));
        var banca = dias[0].GetProperty("ejercicios")[0];
        Assert.Equal(("Press de banca", "Chest", 4, 6, 60m),
            (banca.GetProperty("nombre").GetString(), banca.GetProperty("grupoMuscular").GetString(), banca.GetProperty("series").GetInt32(),
             banca.GetProperty("repeticiones").GetInt32(), banca.GetProperty("pesoKg").GetDecimal()));
        Assert.Equal(JsonValueKind.Null, dias[0].GetProperty("ejercicios")[1].GetProperty("pesoKg").ValueKind);
    }

    [Fact]
    public async Task ConsultarRutinas_NuncaLasDeOtroUsuario()
    {
        var resultado = await ConsultarRutinas(Yo);

        Assert.Contains("Mi torso/pierna", resultado);
        Assert.DoesNotContain("secreta", resultado);
    }

    [Fact]
    public async Task ConsultarRutinas_ElUserIdEsElDeLaApi_NoElDeLosArgumentos()
    {
        var resultado = await ConsultarRutinas(Yo, $$"""{"userId":{{Otro}}}""");

        Assert.Contains("Mi torso/pierna", resultado);
        Assert.DoesNotContain("secreta", resultado);
    }

    [Fact]
    public async Task ConsultarRutinas_NoMandaLaDescripcionLibre()
    {
        var resultado = await ConsultarRutinas(Yo);

        Assert.Contains("Mi torso/pierna", resultado);
        Assert.DoesNotContain("Descripción libre", resultado);
    }
}
