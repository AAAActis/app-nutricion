using AppNutricion.DataAccess.Stores;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;
using AppNutricion.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;

namespace AppNutricion.Tests.Training;

public sealed class EntrenamientoStoresTests : IDisposable
{
    private const int Yo = 1;
    private const int Otro = 2;
    private static readonly DateOnly Dia = new(2026, 10, 20);
    private static readonly DateTime Ahora = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

    private readonly BaseSqlite _db = new(Yo, Otro);
    private readonly Exercise _sentadilla = new() { Name = "Sentadilla", MuscleGroup = MuscleGroup.Legs, MetValue = 5.0m };
    private readonly Exercise _banca = new() { Name = "Press de banca", MuscleGroup = MuscleGroup.Chest, MetValue = 3.5m };
    private readonly Exercise _curl = new() { Name = "Curl de bíceps", MuscleGroup = MuscleGroup.Arms, MetValue = 3.5m };

    public EntrenamientoStoresTests()
    {
        using var context = _db.NuevoContexto();
        context.Exercises.AddRange(_sentadilla, _banca, _curl);
        context.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private Routine Rutina(int? userId, string nombre) => new()
    {
        UserId = userId, Name = nombre, CreatedAt = Ahora,
        Days =
        [
            new RoutineDay
            {
                DayNumber = 2, Name = "Pierna",
                Exercises = [new RoutineExercise { ExerciseId = _sentadilla.Id, Order = 1, TargetSets = 3, TargetReps = 8 }],
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

    private async Task<Routine> Guardar(Routine routine)
    {
        await using var context = _db.NuevoContexto();
        context.Routines.Add(routine);
        await context.SaveChangesAsync();
        return routine;
    }

    private static WorkoutLog Entrenamiento(int userId, DateOnly dia, int? routineDayId = null, params (int ExerciseId, int Reps, decimal Kg)[] sets) => new()
    {
        UserId = userId, RoutineDayId = routineDayId, Date = dia, DurationMinutes = 45, EstimatedCaloriesBurned = 255,
        CreatedAt = Ahora,
        Sets = sets.Select((s, i) => new WorkoutSet { ExerciseId = s.ExerciseId, SetNumber = i + 1, Reps = s.Reps, WeightKg = s.Kg }).ToList(),
    };

    private async Task<WorkoutLog> Guardar(WorkoutLog workout)
    {
        await using var context = _db.NuevoContexto();
        context.WorkoutLogs.Add(workout);
        await context.SaveChangesAsync();
        return workout;
    }

    // ---- Ejercicios ----

    [Fact]
    public async Task Ejercicios_OrdenadosPorNombre_YFiltradosPorGrupo()
    {
        var store = new ExerciseStore(_db.NuevoContexto());

        Assert.Equal(["Curl de bíceps", "Press de banca", "Sentadilla"], (await store.ListAsync(null)).Select(e => e.Name));
        Assert.Equal("Sentadilla", Assert.Single(await store.ListAsync(MuscleGroup.Legs)).Name);
        Assert.Empty(await store.ListAsync(MuscleGroup.Cardio));
    }

    [Fact]
    public async Task Ejercicios_GetByIds_SoloLosQueExisten()
    {
        var encontrados = await new ExerciseStore(_db.NuevoContexto()).GetByIdsAsync([_banca.Id, _curl.Id, 999]);

        Assert.Equal([_banca.Id, _curl.Id], encontrados.Select(e => e.Id).Order());
    }

    [Fact]
    public async Task Ejercicios_AddMissing_InsertaSoloLosNuevosPorNombre()
    {
        var nuevos = new[]
        {
            new Exercise { Name = "Sentadilla", MuscleGroup = MuscleGroup.Legs, MetValue = 9m },
            new Exercise { Name = "Plancha", MuscleGroup = MuscleGroup.Core, MetValue = 2.8m },
        };

        var insertados = await new ExerciseStore(_db.NuevoContexto()).AddMissingAsync(nuevos);
        var otraVez = await new ExerciseStore(_db.NuevoContexto()).AddMissingAsync(nuevos);

        Assert.Equal(1, insertados);
        Assert.Equal(0, otraVez);
        await using var context = _db.NuevoContexto();
        Assert.Equal(4, await context.Exercises.CountAsync());
        Assert.Equal(5.0m, (await context.Exercises.SingleAsync(e => e.Name == "Sentadilla")).MetValue);   // no se pisa
    }

    // ---- Rutinas ----

    [Fact]
    public async Task Rutinas_ListaVisible_PrecargadasYPropias_NuncaLasDeOtro()
    {
        await Guardar(Rutina(Yo, "Mía B"));
        await Guardar(Rutina(null, "Precargada Z"));
        await Guardar(Rutina(Otro, "De otro"));
        await Guardar(Rutina(Yo, "Mía A"));
        await Guardar(Rutina(null, "Precargada A"));

        var rutinas = await new RoutineStore(_db.NuevoContexto()).ListVisibleAsync(Yo);

        Assert.Equal(["Precargada A", "Precargada Z", "Mía A", "Mía B"], rutinas.Select(r => r.Name));
    }

    [Fact]
    public async Task Rutinas_GetVisible_ConDiasYEjerciciosOrdenados()
    {
        var rutina = await Guardar(Rutina(Yo, "Mía"));

        var detalle = await new RoutineStore(_db.NuevoContexto()).GetVisibleAsync(rutina.Id, Yo);

        Assert.NotNull(detalle);
        Assert.Equal([1, 2], detalle.Days.Select(d => d.DayNumber));
        var torso = detalle.Days.First();
        Assert.Equal(["Press de banca", "Curl de bíceps"], torso.Exercises.Select(e => e.Exercise.Name));
        Assert.Equal(60m, torso.Exercises.First().TargetWeightKg);
    }

    [Fact]
    public async Task Rutinas_GetVisible_PrecargadaSi_DeOtroNo()
    {
        var precargada = await Guardar(Rutina(null, "Precargada"));
        var deOtro = await Guardar(Rutina(Otro, "De otro"));
        var store = new RoutineStore(_db.NuevoContexto());

        Assert.NotNull(await store.GetVisibleAsync(precargada.Id, Yo));
        Assert.Null(await store.GetVisibleAsync(deOtro.Id, Yo));
        Assert.Null(await store.GetVisibleAsync(999, Yo));
    }

    [Fact]
    public async Task Rutinas_IsDayVisible_PrecargadaYPropiaSi_DeOtroNo()
    {
        var precargada = await Guardar(Rutina(null, "Precargada"));
        var mia = await Guardar(Rutina(Yo, "Mía"));
        var deOtro = await Guardar(Rutina(Otro, "De otro"));
        var store = new RoutineStore(_db.NuevoContexto());

        Assert.True(await store.IsDayVisibleAsync(precargada.Days.First().Id, Yo));
        Assert.True(await store.IsDayVisibleAsync(mia.Days.First().Id, Yo));
        Assert.False(await store.IsDayVisibleAsync(deOtro.Days.First().Id, Yo));
        Assert.False(await store.IsDayVisibleAsync(999, Yo));
    }

    [Fact]
    public async Task Rutinas_Add_GuardaDiasYEjercicios()
    {
        var rutina = Rutina(Yo, "Nueva");

        await new RoutineStore(_db.NuevoContexto()).AddAsync(rutina);

        Assert.True(rutina.Id > 0);
        await using var context = _db.NuevoContexto();
        Assert.Equal(2, await context.Set<RoutineDay>().CountAsync(d => d.RoutineId == rutina.Id));
        Assert.Equal(3, await context.RoutineExercises.CountAsync());
        Assert.Equal(3, await context.Exercises.CountAsync());   // no duplica el catálogo
    }

    [Fact]
    public async Task Rutinas_DeleteOwn_SoloLaPropia()
    {
        var mia = await Guardar(Rutina(Yo, "Mía"));
        var precargada = await Guardar(Rutina(null, "Precargada"));
        var deOtro = await Guardar(Rutina(Otro, "De otro"));
        var store = new RoutineStore(_db.NuevoContexto());

        Assert.False(await store.DeleteOwnAsync(precargada.Id, Yo));
        Assert.False(await store.DeleteOwnAsync(deOtro.Id, Yo));
        Assert.True(await store.DeleteOwnAsync(mia.Id, Yo));
        Assert.False(await store.DeleteOwnAsync(mia.Id, Yo));

        await using var context = _db.NuevoContexto();
        Assert.Equal(["Precargada", "De otro"], await context.Routines.OrderBy(r => r.Id).Select(r => r.Name).ToListAsync());
        Assert.Equal(4, await context.Set<RoutineDay>().CountAsync());   // los días de la borrada se fueron en cascada
    }

    [Fact]
    public async Task Rutinas_DeleteOwn_ElEntrenamientoQuedaConRoutineDayIdNull()
    {
        var mia = await Guardar(Rutina(Yo, "Mía"));
        var entrenamiento = await Guardar(Entrenamiento(Yo, Dia, mia.Days.First().Id, (_banca.Id, 10, 60)));

        await new RoutineStore(_db.NuevoContexto()).DeleteOwnAsync(mia.Id, Yo);

        await using var context = _db.NuevoContexto();
        var guardado = await context.WorkoutLogs.Include(w => w.Sets).SingleAsync(w => w.Id == entrenamiento.Id);
        Assert.Null(guardado.RoutineDayId);
        Assert.Single(guardado.Sets);
    }

    [Fact]
    public async Task Rutinas_AddMissingPresets_PorNombre_SinMirarLasDeUsuarios()
    {
        await Guardar(Rutina(null, "Cuerpo completo"));
        await Guardar(Rutina(Yo, "Torso/Pierna"));   // una propia con el mismo nombre no cuenta como precargada

        var insertadas = await new RoutineStore(_db.NuevoContexto())
            .AddMissingPresetsAsync([Rutina(null, "Cuerpo completo"), Rutina(null, "Torso/Pierna")]);
        var otraVez = await new RoutineStore(_db.NuevoContexto())
            .AddMissingPresetsAsync([Rutina(null, "Cuerpo completo"), Rutina(null, "Torso/Pierna")]);

        Assert.Equal(1, insertadas);
        Assert.Equal(0, otraVez);
        await using var context = _db.NuevoContexto();
        Assert.Equal(2, await context.Routines.CountAsync(r => r.UserId == null));
    }

    // ---- Entrenamientos ----

    [Fact]
    public async Task Entrenamientos_AddYGet_ConSetsEnOrdenYEjercicio_DeOtroNull()
    {
        var entrenamiento = Entrenamiento(Yo, Dia, null, (_sentadilla.Id, 8, 100), (_banca.Id, 10, 60), (_sentadilla.Id, 6, 110));
        await new WorkoutStore(_db.NuevoContexto()).AddAsync(entrenamiento);
        var store = new WorkoutStore(_db.NuevoContexto());

        var guardado = await store.GetAsync(entrenamiento.Id, Yo);

        Assert.NotNull(guardado);
        Assert.Equal([1, 2, 3], guardado.Sets.Select(s => s.SetNumber));
        Assert.Equal(["Sentadilla", "Press de banca", "Sentadilla"], guardado.Sets.Select(s => s.Exercise.Name));
        Assert.Null(await store.GetAsync(entrenamiento.Id, Otro));
    }

    [Fact]
    public async Task Entrenamientos_Lista_DelRango_MasNuevoPrimero_ConSeriesYVolumen()
    {
        var viejo = await Guardar(Entrenamiento(Yo, Dia.AddDays(-2), null, (_banca.Id, 10, 60m)));
        var nuevo = await Guardar(Entrenamiento(Yo, Dia, null, (_sentadilla.Id, 8, 100m), (_sentadilla.Id, 6, 102.5m), (_curl.Id, 12, 0m)));
        await Guardar(Entrenamiento(Yo, Dia.AddDays(-3), null, (_banca.Id, 1, 1m)));    // fuera del rango
        await Guardar(Entrenamiento(Otro, Dia, null, (_banca.Id, 1, 1m)));             // de otro

        var lista = await new WorkoutStore(_db.NuevoContexto()).ListAsync(Yo, Dia.AddDays(-2), Dia);

        Assert.Equal([nuevo.Id, viejo.Id], lista.Select(w => w.Id));
        Assert.Equal(3, lista[0].SetCount);
        Assert.Equal(8 * 100m + 6 * 102.5m, lista[0].VolumeKg);
        Assert.Equal(255m, lista[0].EstimatedCaloriesBurned);
        Assert.Equal(45, lista[0].DurationMinutes);
        Assert.Equal(Dia, lista[0].Date);
        Assert.Equal((1, 600m), (lista[1].SetCount, lista[1].VolumeKg));
    }

    [Fact]
    public async Task Entrenamientos_Delete_SoloElPropio()
    {
        var mio = await Guardar(Entrenamiento(Yo, Dia, null, (_banca.Id, 10, 60m)));
        var store = new WorkoutStore(_db.NuevoContexto());

        Assert.False(await store.DeleteAsync(mio.Id, Otro));
        Assert.True(await store.DeleteAsync(mio.Id, Yo));

        await using var context = _db.NuevoContexto();
        Assert.Equal(0, await context.WorkoutLogs.CountAsync());
        Assert.Equal(0, await context.Set<WorkoutSet>().CountAsync());
    }
}
