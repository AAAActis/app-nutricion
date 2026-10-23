using AppNutricion.BusinessLogic.Services;
using AppNutricion.DataAccess.Stores;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;
using AppNutricion.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;

namespace AppNutricion.Tests.Training;

public sealed class TrainingSeedServiceTests : IDisposable
{
    private readonly BaseSqlite _db = new();

    public void Dispose() => _db.Dispose();

    private TrainingSeedService NuevoServicio() =>
        new(new ExerciseStore(_db.NuevoContexto()), new RoutineStore(_db.NuevoContexto()));

    private static string ArchivoReal()
    {
        var dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "AppNutricion.slnx"))) dir = Path.GetDirectoryName(dir)!;
        return File.ReadAllText(Path.Combine(dir, "AppNutricion.Api", "SeedData", "exercises_seed.json"));
    }

    [Fact]
    public async Task ArchivoReal_DosCargas_LaSegundaNoInsertaNada()
    {
        var json = ArchivoReal();

        var primera = await NuevoServicio().SeedAsync(json);
        var segunda = await NuevoServicio().SeedAsync(json);

        Assert.InRange(primera.ExercisesInserted, 20, 30);
        Assert.Equal(3, primera.RoutinesInserted);
        Assert.Equal((0, 0), (segunda.ExercisesInserted, segunda.RoutinesInserted));
        await using var context = _db.NuevoContexto();
        Assert.Equal(primera.ExercisesInserted, await context.Exercises.CountAsync());
        Assert.Equal(3, await context.Routines.CountAsync());
    }

    [Fact]
    public async Task ArchivoReal_TresPrecargadasConSusDias_SinPesoObjetivo()
    {
        await NuevoServicio().SeedAsync(ArchivoReal());

        await using var context = _db.NuevoContexto();
        var rutinas = await context.Routines.Include(r => r.Days).ThenInclude(d => d.Exercises).OrderBy(r => r.Id).ToListAsync();
        Assert.Equal(
            [("Principiante cuerpo completo (2 días)", 2), ("Cuerpo completo (3 días)", 3), ("Torso/Pierna (4 días)", 4)],
            rutinas.Select(r => (r.Name, r.Days.Count)));
        Assert.All(rutinas, r => Assert.Null(r.UserId));
        var ejercicios = rutinas.SelectMany(r => r.Days).SelectMany(d => d.Exercises).ToList();
        Assert.All(ejercicios, e => Assert.Null(e.TargetWeightKg));
        Assert.All(ejercicios, e => Assert.InRange(e.TargetSets, 1, 10));
        Assert.All(ejercicios, e => Assert.InRange(e.TargetReps, 1, 100));
    }

    [Fact]
    public async Task ArchivoReal_EjerciciosConGrupoYMetPositivo()
    {
        await NuevoServicio().SeedAsync(ArchivoReal());

        await using var context = _db.NuevoContexto();
        var sentadilla = await context.Exercises.SingleAsync(e => e.Name == "Sentadilla");
        Assert.Equal((MuscleGroup.Legs, 5.0m), (sentadilla.MuscleGroup, sentadilla.MetValue));
        Assert.All(await context.Exercises.ToListAsync(), e => Assert.InRange(e.MetValue, 1m, 20m));
    }

    [Fact]
    public async Task EjercicioExistente_NoSePisa()
    {
        await using (var context = _db.NuevoContexto())
        {
            context.Exercises.Add(new Exercise { Name = "Sentadilla", MuscleGroup = MuscleGroup.Legs, MetValue = 9m });
            await context.SaveChangesAsync();
        }

        var resumen = await NuevoServicio().SeedAsync(ArchivoReal());

        await using var verificar = _db.NuevoContexto();
        Assert.Equal(await verificar.Exercises.CountAsync() - 1, resumen.ExercisesInserted);
        Assert.Equal(9m, (await verificar.Exercises.SingleAsync(e => e.Name == "Sentadilla")).MetValue);
    }

    [Fact]
    public async Task RutinaConEjercicioQueNoEstaEnElCatalogo_Falla()
    {
        const string json = """
            { "exercises": [ { "name": "Sentadilla", "muscleGroup": "Legs", "metValue": 5.0 } ],
              "routines": [ { "name": "R", "days": [ { "dayNumber": 1, "name": "A",
                "exercises": [ { "exercise": "Inventado", "order": 1, "targetSets": 3, "targetReps": 10 } ] } ] } ] }
            """;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => NuevoServicio().SeedAsync(json));

        Assert.Contains("Inventado", ex.Message);
    }

    [Fact]
    public async Task GrupoMuscularDesconocido_Falla()
    {
        const string json = """{ "exercises": [ { "name": "X", "muscleGroup": "Piernas", "metValue": 5.0 } ], "routines": [] }""";

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => NuevoServicio().SeedAsync(json));

        Assert.Contains("Piernas", ex.Message);
    }
}
