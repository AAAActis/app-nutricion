using AppNutricion.BusinessLogic.Exceptions;
using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.BusinessLogic.Managers;
using AppNutricion.BusinessLogic.Services;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;
using AppNutricion.Tests.Infraestructura;
using Moq;

namespace AppNutricion.Tests.Training;

public class WorkoutManagerTests
{
    private const int UserId = 7;
    private static readonly DateTimeOffset Ahora = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Hoy = new(2026, 10, 22);

    private readonly Mock<IWorkoutStore> _workouts = new();
    private readonly Mock<IExerciseStore> _exercises = new();
    private readonly Mock<IRoutineStore> _routines = new();
    private readonly Mock<IBodyMetricStore> _metrics = new();
    private readonly WorkoutManager _manager;
    private readonly List<Exercise> _catalogo =
    [
        new() { Id = 1, Name = "Sentadilla", MuscleGroup = MuscleGroup.Legs, MetValue = 5.0m },
        new() { Id = 2, Name = "Curl de bíceps", MuscleGroup = MuscleGroup.Arms, MetValue = 3.5m },
        new() { Id = 3, Name = "Remo ergómetro", MuscleGroup = MuscleGroup.Cardio, MetValue = 7.3m },
    ];
    private WorkoutLog? _agregado;

    public WorkoutManagerTests()
    {
        _exercises.Setup(s => s.GetByIdsAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync((IEnumerable<int> ids) => _catalogo.Where(e => ids.Contains(e.Id)).ToList());
        PesoActual(80m);
        _workouts.Setup(s => s.AddAsync(It.IsAny<WorkoutLog>()))
            .Callback((WorkoutLog w) => { w.Id = 50; _agregado = w; });
        _workouts.Setup(s => s.GetAsync(50, UserId)).ReturnsAsync(() => _agregado);
        // Sin perfil: "hoy" es UTC y se tolera un día más por las zonas adelantadas (IUserToday de Lauti).
        var reloj = new RelojFijo(Ahora);
        _manager = new WorkoutManager(_workouts.Object, _exercises.Object, _routines.Object, _metrics.Object,
            new UserToday(Mock.Of<IProfileStore>(), reloj), reloj);
    }

    private void PesoActual(decimal? kg) =>
        _metrics.Setup(s => s.GetLatestAsync(UserId, 1))
            .ReturnsAsync(kg is { } w ? [new BodyMetric { UserId = UserId, Date = Hoy, WeightKg = w }] : []);

    private static WorkoutSetDraft Set(int exerciseId = 1, int setNumber = 1, int reps = 10, decimal kg = 60) =>
        new(exerciseId, setNumber, reps, kg);

    private static WorkoutDraft Valido(List<WorkoutSetDraft>? sets = null, int minutos = 45) =>
        new(null, Hoy, minutos, "Bien", sets ?? [Set()]);

    private async Task<ArgumentException> Rechaza(WorkoutDraft draft)
    {
        var ex = await Assert.ThrowsAnyAsync<ArgumentException>(() => _manager.CreateAsync(UserId, draft));
        _workouts.Verify(s => s.AddAsync(It.IsAny<WorkoutLog>()), Times.Never);
        return ex;
    }

    // ---- kcal ----

    // A mano: MET promedio de los ejercicios distintos (5,0 + 3,5) / 2 = 4,25 (no pesa que la sentadilla tenga 3 sets)
    // × 80 kg × 45 min / 60 = 4,25 × 80 × 0,75 = 255 kcal.
    [Fact]
    public async Task Create_KcalEsMetPromedioDeEjerciciosDistintosPorPesoPorHoras()
    {
        var creado = await _manager.CreateAsync(UserId, Valido([Set(1, 1), Set(1, 2), Set(1, 3), Set(2, 4)], minutos: 45));

        Assert.NotNull(creado);
        Assert.Equal(255m, creado.EstimatedCaloriesBurned);
    }

    // 7,3 × 50 kg × 6 min / 60 = 36,5 → 37 (redondeo a entero, el ,5 hacia arriba).
    [Fact]
    public async Task Create_KcalRedondeadaAEntero_ElMedioHaciaArriba()
    {
        PesoActual(50m);

        var creado = await _manager.CreateAsync(UserId, Valido([Set(3, kg: 0)], minutos: 6));

        Assert.Equal(37m, creado!.EstimatedCaloriesBurned);
    }

    // Con el último peso: 3,5 × 70,3 × 50 / 60 = 205,04… → 205.
    [Fact]
    public async Task Create_UsaElUltimoPesoDelUsuario()
    {
        PesoActual(70.3m);

        var creado = await _manager.CreateAsync(UserId, Valido([Set(2)], minutos: 50));

        Assert.Equal(205m, creado!.EstimatedCaloriesBurned);
        _metrics.Verify(s => s.GetLatestAsync(UserId, 1), Times.Once);
    }

    [Fact]
    public async Task Create_SinPeso_ConflictExceptionYNoGuarda()
    {
        PesoActual(null);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => _manager.CreateAsync(UserId, Valido()));

        Assert.Equal("Primero registrá tu peso.", ex.Message);
        _workouts.Verify(s => s.AddAsync(It.IsAny<WorkoutLog>()), Times.Never);
    }

    // ---- Alta ----

    [Fact]
    public async Task Create_GuardaTodoConElUserIdYDevuelveLoGuardado()
    {
        var creado = await _manager.CreateAsync(UserId, new WorkoutDraft(null, Hoy.AddDays(-1), 45, "  Piernas  ",
            [Set(1, 1, 8, 100m), Set(2, 2, 12, 12.5m)]));

        Assert.NotNull(_agregado);
        Assert.Same(_agregado, creado);
        Assert.Equal(UserId, _agregado.UserId);
        Assert.Null(_agregado.RoutineDayId);
        Assert.Equal(Hoy.AddDays(-1), _agregado.Date);
        Assert.Equal(45, _agregado.DurationMinutes);
        Assert.Equal("Piernas", _agregado.Notes);
        Assert.Equal(Ahora.UtcDateTime, _agregado.CreatedAt);
        Assert.Equal(DateTimeKind.Utc, _agregado.CreatedAt.Kind);
        Assert.Equal([(1, 1, 8, 100m), (2, 2, 12, 12.5m)],
            _agregado.Sets.Select(s => (s.ExerciseId, s.SetNumber, s.Reps, s.WeightKg)));
        _workouts.Verify(s => s.GetAsync(50, UserId), Times.Once);
    }

    [Fact]
    public async Task Create_NotasVaciasQuedanNull()
    {
        await _manager.CreateAsync(UserId, Valido() with { Notes = "  " });

        Assert.Null(_agregado!.Notes);
    }

    [Fact]
    public async Task Create_NotasDeHasta500()
    {
        Assert.Equal("notes", (await Rechaza(Valido() with { Notes = new string('a', 501) })).ParamName);
    }

    [Fact]
    public async Task Create_ConDiaDeRutinaVisible_LoGuarda()
    {
        _routines.Setup(s => s.IsDayVisibleAsync(9, UserId)).ReturnsAsync(true);

        await _manager.CreateAsync(UserId, Valido() with { RoutineDayId = 9 });

        Assert.Equal(9, _agregado!.RoutineDayId);
    }

    [Fact]
    public async Task Create_DiaDeRutinaNoVisible_NullYNoGuarda()
    {
        _routines.Setup(s => s.IsDayVisibleAsync(9, UserId)).ReturnsAsync(false);

        Assert.Null(await _manager.CreateAsync(UserId, Valido() with { RoutineDayId = 9 }));
        _workouts.Verify(s => s.AddAsync(It.IsAny<WorkoutLog>()), Times.Never);
    }

    // ---- Validaciones ----

    [Fact]
    public async Task Create_FechaConLasReglasDelDiario()
    {
        Assert.Equal("date", (await Rechaza(Valido() with { Date = Hoy.AddDays(2) })).ParamName);
        Assert.Equal("date", (await Rechaza(Valido() with { Date = Hoy.AddYears(-2).AddDays(-1) })).ParamName);
        Assert.Equal("date", (await Rechaza(Valido() with { Date = default })).ParamName);
    }

    [Fact]
    public async Task Create_DuracionDeUnoA600Minutos()
    {
        Assert.Equal("durationMinutes", (await Rechaza(Valido(minutos: 0))).ParamName);
        Assert.Equal("durationMinutes", (await Rechaza(Valido(minutos: 601))).ParamName);

        await _manager.CreateAsync(UserId, Valido(minutos: 1));
        await _manager.CreateAsync(UserId, Valido(minutos: 600));
        Assert.Equal(600, _agregado!.DurationMinutes);
    }

    [Fact]
    public async Task Create_DeUnoACienSets()
    {
        Assert.Equal("sets", (await Rechaza(Valido() with { Sets = null })).ParamName);
        Assert.Equal("sets", (await Rechaza(Valido([]))).ParamName);
        Assert.Equal("sets", (await Rechaza(Valido(Enumerable.Range(1, 101).Select(n => Set(setNumber: n)).ToList()))).ParamName);

        await _manager.CreateAsync(UserId, Valido(Enumerable.Range(1, 100).Select(n => Set(setNumber: n)).ToList()));
        Assert.Equal(100, _agregado!.Sets.Count);
    }

    [Theory]
    [InlineData(0, 10, 60, "setNumber")]
    [InlineData(-1, 10, 60, "setNumber")]
    [InlineData(2, 0, 60, "reps")]
    [InlineData(2, 101, 60, "reps")]
    [InlineData(2, 10, -0.01, "weightKg")]
    [InlineData(2, 10, 500.01, "weightKg")]
    public async Task Create_LimitesDelSet_RechazaConElCampo(int setNumber, int reps, double kg, string campo)
    {
        var draft = Valido([Set(), Set(setNumber: setNumber, reps: reps, kg: (decimal)kg)]);

        Assert.Equal($"sets[1].{campo}", (await Rechaza(draft)).ParamName);
    }

    [Theory]
    [InlineData(1, 1, 0)]
    [InlineData(1, 100, 500)]
    public async Task Create_EnLosLimitesDelSet_EsValido(int setNumber, int reps, double kg)
    {
        await _manager.CreateAsync(UserId, Valido([Set(setNumber: setNumber, reps: reps, kg: (decimal)kg)]));

        Assert.NotNull(_agregado);
    }

    [Fact]
    public async Task Create_EjercicioInexistente_RechazaConElCampoExerciseId()
    {
        Assert.Equal("sets[1].exerciseId", (await Rechaza(Valido([Set(1), Set(99, 2)]))).ParamName);
    }

    // ---- Lista, detalle y baja ----

    [Fact]
    public async Task List_RangoObligatorio_DeHasta366Dias()
    {
        Assert.Equal("from", (await Assert.ThrowsAnyAsync<ArgumentException>(() => _manager.ListAsync(UserId, null, Hoy))).ParamName);
        Assert.Equal("to", (await Assert.ThrowsAnyAsync<ArgumentException>(() => _manager.ListAsync(UserId, Hoy, null))).ParamName);
        Assert.Equal("to", (await Assert.ThrowsAnyAsync<ArgumentException>(() => _manager.ListAsync(UserId, Hoy, Hoy.AddDays(-1)))).ParamName);
        Assert.Equal("to", (await Assert.ThrowsAnyAsync<ArgumentException>(() => _manager.ListAsync(UserId, Hoy.AddDays(-366), Hoy))).ParamName);
        _workouts.Verify(s => s.ListAsync(It.IsAny<int>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>()), Times.Never);
    }

    [Fact]
    public async Task List_Con366Dias_ConsultaElStoreConElUserId()
    {
        var lista = new List<WorkoutSummary>();
        _workouts.Setup(s => s.ListAsync(UserId, Hoy.AddDays(-365), Hoy)).ReturnsAsync(lista);

        Assert.Same(lista, await _manager.ListAsync(UserId, Hoy.AddDays(-365), Hoy));
    }

    [Fact]
    public async Task GetYDelete_SiempreConElUserId()
    {
        var w = new WorkoutLog { Id = 4, UserId = UserId };
        _workouts.Setup(s => s.GetAsync(4, UserId)).ReturnsAsync(w);
        _workouts.Setup(s => s.DeleteAsync(4, UserId)).ReturnsAsync(true);

        Assert.Same(w, await _manager.GetAsync(4, UserId));
        Assert.Null(await _manager.GetAsync(5, UserId));
        Assert.True(await _manager.DeleteAsync(4, UserId));
        Assert.False(await _manager.DeleteAsync(5, UserId));
    }
}
