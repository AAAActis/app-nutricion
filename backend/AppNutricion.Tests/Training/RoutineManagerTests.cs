using AppNutricion.BusinessLogic.Exceptions;
using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.BusinessLogic.Managers;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;
using AppNutricion.Tests.Infraestructura;
using Moq;

namespace AppNutricion.Tests.Training;

public class RoutineManagerTests
{
    private const int UserId = 7;
    private static readonly DateTimeOffset Ahora = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IRoutineStore> _routines = new();
    private readonly Mock<IExerciseStore> _exercises = new();
    private readonly RoutineManager _manager;
    private readonly List<Exercise> _catalogo =
    [
        new() { Id = 1, Name = "Sentadilla", MuscleGroup = MuscleGroup.Legs, MetValue = 5.0m },
        new() { Id = 2, Name = "Curl de bíceps", MuscleGroup = MuscleGroup.Arms, MetValue = 3.5m },
    ];
    private Routine? _agregada;

    public RoutineManagerTests()
    {
        _exercises.Setup(s => s.GetByIdsAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync((IEnumerable<int> ids) => _catalogo.Where(e => ids.Contains(e.Id)).ToList());
        _routines.Setup(s => s.AddAsync(It.IsAny<Routine>()))
            .Callback((Routine r) => { r.Id = 40; _agregada = r; });
        _manager = new RoutineManager(_routines.Object, _exercises.Object, new RelojFijo(Ahora));
    }

    private static RoutineExerciseDraft Ejercicio(int exerciseId = 1, int order = 1, int sets = 3, int reps = 10, decimal? kg = null) =>
        new(exerciseId, order, sets, reps, kg);

    private static RoutineDayDraft Dia(int dayNumber = 1, string? name = "Día A", List<RoutineExerciseDraft>? exercises = null) =>
        new(dayNumber, name, exercises ?? [Ejercicio()]);

    private static RoutineDraft Valida(List<RoutineDayDraft>? days = null) => new("Mi rutina", "Para empezar", days ?? [Dia()]);

    private async Task<ArgumentException> Rechaza(RoutineDraft draft)
    {
        var ex = await Assert.ThrowsAnyAsync<ArgumentException>(() => _manager.CreateAsync(UserId, draft));
        _routines.Verify(s => s.AddAsync(It.IsAny<Routine>()), Times.Never);
        return ex;
    }

    // ---- Catálogo ----

    [Theory]
    [InlineData(null, null)]
    [InlineData("Legs", MuscleGroup.Legs)]
    [InlineData("cardio", MuscleGroup.Cardio)]
    public async Task ListExercises_FiltraPorGrupoSinDistinguirMayusculas(string? texto, MuscleGroup? grupo)
    {
        _exercises.Setup(s => s.ListAsync(grupo)).ReturnsAsync(_catalogo);

        Assert.Same(_catalogo, await _manager.ListExercisesAsync(texto));
    }

    [Theory]
    [InlineData("piernas")]
    [InlineData("1")]
    [InlineData("")]
    public async Task ListExercises_GrupoInvalido_ArgumentExceptionConElCampo(string texto)
    {
        var ex = await Assert.ThrowsAnyAsync<ArgumentException>(() => _manager.ListExercisesAsync(texto));

        Assert.Equal("muscleGroup", ex.ParamName);
        _exercises.Verify(s => s.ListAsync(It.IsAny<MuscleGroup?>()), Times.Never);
    }

    // ---- Lista y detalle ----

    [Fact]
    public async Task ListYGet_SiempreConElUserIdDelUsuario()
    {
        var lista = new List<Routine>();
        var rutina = new Routine { Id = 3, Name = "x" };
        _routines.Setup(s => s.ListVisibleAsync(UserId)).ReturnsAsync(lista);
        _routines.Setup(s => s.GetVisibleAsync(3, UserId)).ReturnsAsync(rutina);

        Assert.Same(lista, await _manager.ListAsync(UserId));
        Assert.Same(rutina, await _manager.GetAsync(3, UserId));
        Assert.Null(await _manager.GetAsync(4, UserId));
    }

    // ---- Alta ----

    [Fact]
    public async Task Create_Valida_GuardaLaRutinaPropiaYDevuelveElDetalleGuardado()
    {
        var detalle = new Routine { Id = 40, Name = "Mi rutina" };
        _routines.Setup(s => s.GetVisibleAsync(40, UserId)).ReturnsAsync(detalle);

        var creada = await _manager.CreateAsync(UserId, new RoutineDraft("  Mi rutina  ", "Para empezar",
        [
            Dia(2, " Pierna ", [Ejercicio(1, 1, 4, 6, 100m)]),
            Dia(1, "Brazos", [Ejercicio(2, 1, 3, 12), Ejercicio(1, 2, 2, 8, 0m)]),
        ]));

        Assert.Same(detalle, creada);
        Assert.NotNull(_agregada);
        Assert.Equal(UserId, _agregada.UserId);
        Assert.Equal("Mi rutina", _agregada.Name);
        Assert.Equal("Para empezar", _agregada.Description);
        Assert.Equal(Ahora.UtcDateTime, _agregada.CreatedAt);
        Assert.Equal(DateTimeKind.Utc, _agregada.CreatedAt.Kind);
        Assert.Equal([(2, "Pierna"), (1, "Brazos")], _agregada.Days.Select(d => (d.DayNumber, d.Name)));
        var pierna = Assert.Single(_agregada.Days.First().Exercises);
        Assert.Equal((1, 1, 4, 6, (decimal?)100m), (pierna.ExerciseId, pierna.Order, pierna.TargetSets, pierna.TargetReps, pierna.TargetWeightKg));
        Assert.Equal([(2, (decimal?)null), (1, 0m)], _agregada.Days.Last().Exercises.Select(e => (e.ExerciseId, e.TargetWeightKg)));
    }

    [Fact]
    public async Task Create_SinDescripcion_QuedaNull()
    {
        await _manager.CreateAsync(UserId, Valida() with { Description = "   " });

        Assert.Null(_agregada!.Description);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_SinNombre_Rechaza(string? nombre)
    {
        Assert.Equal("name", (await Rechaza(Valida() with { Name = nombre })).ParamName);
    }

    [Fact]
    public async Task Create_LargosDeNombreYDescripcion()
    {
        Assert.Equal("name", (await Rechaza(Valida() with { Name = new string('a', 121) })).ParamName);
        Assert.Equal("description", (await Rechaza(Valida() with { Description = new string('a', 501) })).ParamName);

        await _manager.CreateAsync(UserId, Valida() with { Name = new string('a', 120), Description = new string('a', 500) });
        Assert.NotNull(_agregada);
    }

    [Fact]
    public async Task Create_DeUnoASieteDias()
    {
        Assert.Equal("days", (await Rechaza(Valida() with { Days = null })).ParamName);
        Assert.Equal("days", (await Rechaza(Valida([]))).ParamName);
        Assert.Equal("days", (await Rechaza(Valida(Enumerable.Range(1, 8).Select(n => Dia(n)).ToList()))).ParamName);

        await _manager.CreateAsync(UserId, Valida(Enumerable.Range(1, 7).Select(n => Dia(n)).ToList()));
        Assert.Equal(7, _agregada!.Days.Count);
    }

    [Fact]
    public async Task Create_DayNumberRepetidoOMenorAUno_Rechaza()
    {
        Assert.Equal("days[1].dayNumber", (await Rechaza(Valida([Dia(1), Dia(1)]))).ParamName);
        Assert.Equal("days[0].dayNumber", (await Rechaza(Valida([Dia(0)]))).ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public async Task Create_DiaSinNombre_Rechaza(string? nombre)
    {
        Assert.Equal("days[0].name", (await Rechaza(Valida([Dia(name: nombre)]))).ParamName);
    }

    [Fact]
    public async Task Create_NombreDelDiaDeHasta60()
    {
        Assert.Equal("days[0].name", (await Rechaza(Valida([Dia(name: new string('a', 61))]))).ParamName);

        await _manager.CreateAsync(UserId, Valida([Dia(name: new string('a', 60))]));
        Assert.NotNull(_agregada);
    }

    [Fact]
    public async Task Create_DeUnoAQuinceEjerciciosPorDia()
    {
        Assert.Equal("days[0].exercises", (await Rechaza(Valida([new RoutineDayDraft(1, "A", null)]))).ParamName);
        Assert.Equal("days[0].exercises", (await Rechaza(Valida([Dia(exercises: [])]))).ParamName);
        Assert.Equal("days[0].exercises",
            (await Rechaza(Valida([Dia(exercises: Enumerable.Range(1, 16).Select(n => Ejercicio(order: n)).ToList())]))).ParamName);

        await _manager.CreateAsync(UserId, Valida([Dia(exercises: Enumerable.Range(1, 15).Select(n => Ejercicio(order: n)).ToList())]));
        Assert.Equal(15, _agregada!.Days.Single().Exercises.Count);
    }

    [Theory]
    [InlineData(0, 10, null, "targetSets")]
    [InlineData(11, 10, null, "targetSets")]
    [InlineData(3, 0, null, "targetReps")]
    [InlineData(3, 101, null, "targetReps")]
    [InlineData(3, 10, -0.01, "targetWeightKg")]
    [InlineData(3, 10, 500.01, "targetWeightKg")]
    public async Task Create_LimitesDeSeriesRepsYPeso_Rechaza(int sets, int reps, double? kg, string campo)
    {
        var draft = Valida([Dia(2, exercises: [Ejercicio(), Ejercicio(sets: sets, reps: reps, kg: (decimal?)kg)]), Dia(1)]);

        Assert.Equal($"days[0].exercises[1].{campo}", (await Rechaza(draft)).ParamName);
    }

    [Theory]
    [InlineData(1, 1, 0)]
    [InlineData(10, 100, 500)]
    public async Task Create_EnLosLimitesDeSeriesRepsYPeso_EsValida(int sets, int reps, double kg)
    {
        await _manager.CreateAsync(UserId, Valida([Dia(exercises: [Ejercicio(sets: sets, reps: reps, kg: (decimal)kg)])]));

        Assert.NotNull(_agregada);
    }

    [Fact]
    public async Task Create_EjercicioInexistente_RechazaConElCampoExerciseId()
    {
        var draft = Valida([Dia(1), Dia(2, exercises: [Ejercicio(1), Ejercicio(99)])]);

        Assert.Equal("days[1].exercises[1].exerciseId", (await Rechaza(draft)).ParamName);
    }

    // ---- Baja ----

    [Fact]
    public async Task Delete_Propia_LaBorraConElUserId()
    {
        _routines.Setup(s => s.GetVisibleAsync(3, UserId)).ReturnsAsync(new Routine { Id = 3, UserId = UserId, Name = "Mía" });
        _routines.Setup(s => s.DeleteOwnAsync(3, UserId)).ReturnsAsync(true);

        Assert.True(await _manager.DeleteAsync(3, UserId));
        _routines.Verify(s => s.DeleteOwnAsync(3, UserId), Times.Once);
    }

    [Fact]
    public async Task Delete_Precargada_ConflictException()
    {
        _routines.Setup(s => s.GetVisibleAsync(3, UserId)).ReturnsAsync(new Routine { Id = 3, UserId = null, Name = "Precargada" });

        var ex = await Assert.ThrowsAsync<ConflictException>(() => _manager.DeleteAsync(3, UserId));

        Assert.Equal("Las rutinas precargadas no se pueden borrar", ex.Message);
        _routines.Verify(s => s.DeleteOwnAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Delete_DeOtroOInexistente_False()
    {
        Assert.False(await _manager.DeleteAsync(3, UserId));
        _routines.Verify(s => s.DeleteOwnAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }
}
