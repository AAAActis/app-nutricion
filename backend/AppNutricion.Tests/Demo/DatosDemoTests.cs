using AppNutricion.BusinessLogic.Constants;
using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.BusinessLogic.Registrations;
using AppNutricion.BusinessLogic.Services;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Enums;
using AppNutricion.Tests.Foods;
using AppNutricion.Tests.Infraestructura;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace AppNutricion.Tests.Demo;

// La API entera sobre SQLite, con los seeds reales de alimentos y rutinas y el reloj fijo: el 2026-03-10 a las 12:00 en
// Buenos Aires (la zona del perfil demo).
public class ApiFactoryDemo : ApiFactorySqlite
{
    public static readonly DateOnly Hoy = new(2026, 3, 10);
    public FakeTimeProvider Reloj { get; } = new(new DateTimeOffset(2026, 3, 10, 15, 0, 0, TimeSpan.Zero));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Reloj);
            services.Replace(ServiceDescriptor.Scoped<IFoodStore, FoodStoreSinIlike>());
        });
    }

    public async Task SembrarCatalogosAsync()
    {
        using var scope = Services.CreateScope();
        var carpeta = Path.Combine(AppContext.BaseDirectory, "SeedData");
        await scope.ServiceProvider.GetRequiredService<IFoodSeedService>().SeedAsync(await File.ReadAllTextAsync(Path.Combine(carpeta, "foods_seed.json")));
        await scope.ServiceProvider.GetRequiredService<ITrainingSeedService>().SeedAsync(await File.ReadAllTextAsync(Path.Combine(carpeta, "exercises_seed.json")));
    }

    public async Task<DemoSeedSummary> DemoAsync()
    {
        using var scope = Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IDemoDataService>().SeedAsync();
    }

    public async Task<int> DemoUserIdAsync()
    {
        await using var context = NuevoContexto();
        var user = await context.Users.SingleOrDefaultAsync(u => u.Email == DemoDataService.DemoEmail);
        Assert.NotNull(user);
        return user.Id;
    }
}

public class DatosDemoTests
{
    private static readonly string[] AlimentosDeLaDemo =
    [
        "Pollo, asado al horno", "Arroz, grano, blanco, pulido, hervido", "Huevo de gallina, frito", "Trigo, pan francés",
        "Banana, pulpa, fresca", "Avena, grano, arrollada, cruda", "Queso cremoso",
    ];

    // ---- El HostedService corre solo con Demo:Enabled = true ----

    private static (DemoDataHostedService Servicio, Mock<IDemoDataService> Demo, ListLogger<DemoDataHostedService> Logger) Hosted(string? enabled)
    {
        var demo = new Mock<IDemoDataService>();
        demo.Setup(d => d.SeedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new DemoSeedSummary(true, 14, 40, 3, true, true));
        var services = new ServiceCollection().AddScoped(_ => demo.Object).BuildServiceProvider();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Demo:Enabled"] = enabled }).Build();
        var logger = new ListLogger<DemoDataHostedService>();
        return (new DemoDataHostedService(services.GetRequiredService<IServiceScopeFactory>(), config, logger), demo, logger);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("false")]
    [InlineData("no")]
    public async Task HostedService_SinDemoEnabledTrue_NoCargaNada(string? enabled)
    {
        var (servicio, demo, _) = Hosted(enabled);

        await servicio.StartAsync(CancellationToken.None);

        demo.Verify(d => d.SeedAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("true")]
    [InlineData("True")]
    public async Task HostedService_ConDemoEnabledTrue_CargaUnaVezYLoAvisa(string enabled)
    {
        var (servicio, demo, logger) = Hosted(enabled);

        await servicio.StartAsync(CancellationToken.None);

        demo.Verify(d => d.SeedAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains(logger.Entries, e => e.Message.Contains(DemoDataService.DemoEmail));
    }

    [Fact]
    public async Task HostedService_SiFalla_LoLogueaYLaApiSigue()
    {
        var (servicio, demo, logger) = Hosted("true");
        demo.Setup(d => d.SeedAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("sin alimentos"));

        await servicio.StartAsync(CancellationToken.None);

        Assert.Contains(logger.Entries, e => e.Level == Microsoft.Extensions.Logging.LogLevel.Error && e.Message.Contains("demo"));
    }

    [Fact]
    public void Registro_DespuesDeLosSeedsDeAlimentosYRutinas()
    {
        var services = new ServiceCollection().AddBusinessLogic();

        var hosted = services.Where(d => d.ServiceType == typeof(IHostedService)).Select(d => d.ImplementationType).ToList();
        Assert.Contains(typeof(DemoDataHostedService), hosted);
        Assert.True(hosted.IndexOf(typeof(DemoDataHostedService)) > hosted.IndexOf(typeof(TrainingSeedHostedService)));
        Assert.True(hosted.IndexOf(typeof(DemoDataHostedService)) > hosted.IndexOf(typeof(FoodSeedHostedService)));
        Assert.Contains(services, d => d.ServiceType == typeof(IDemoDataService) && d.ImplementationType == typeof(DemoDataService));
    }

    [Fact]
    public void Appsettings_DemoApagadaPorDefecto_YPrendidaSoloEnDevelopment()
    {
        using var testing = new ApiFactory();
        using var desarrollo = new ApiFactoryDesarrollo();

        Assert.Equal("false", testing.Services.GetRequiredService<IConfiguration>()["Demo:Enabled"]);
        Assert.Equal("true", desarrollo.Services.GetRequiredService<IConfiguration>()["Demo:Enabled"]);
    }

    // ---- Los datos ----

    [Fact]
    public async Task UsuarioDemo_ConConsentimientoYPerfil_YEntraConSuContraseña()
    {
        using var factory = new ApiFactoryDemo();
        await factory.SembrarCatalogosAsync();

        var resumen = await factory.DemoAsync();

        Assert.True(resumen.UserCreated);
        await using var context = factory.NuevoContexto();
        var user = await context.Users.SingleAsync(u => u.Email == DemoDataService.DemoEmail);
        Assert.Equal(ConsentVersions.Current, user.ConsentVersion);
        Assert.NotNull(await context.Profiles.SingleOrDefaultAsync(p => p.UserId == user.Id));
        using var scope = factory.Services.CreateScope();
        Assert.NotNull(await scope.ServiceProvider.GetRequiredService<IAuthManager>().LoginAsync(DemoDataService.DemoEmail, DemoDataService.DemoPassword));
    }

    [Fact]
    public async Task Peso_14DiasHastaHoy_ConTendenciaALaBaja()
    {
        using var factory = new ApiFactoryDemo();
        await factory.SembrarCatalogosAsync();
        await factory.DemoAsync();
        var userId = await factory.DemoUserIdAsync();

        using var scope = factory.Services.CreateScope();
        var historial = await scope.ServiceProvider.GetRequiredService<IBodyMetricManager>().GetHistoryAsync(userId, null, null);

        Assert.Equal(Enumerable.Range(0, 14).Select(i => ApiFactoryDemo.Hoy.AddDays(i - 13)), historial.Metrics.Select(m => m.Date).Order());
        Assert.NotNull(historial.Trend);
        Assert.True(historial.Trend.ChangeKg < 0, $"ChangeKg = {historial.Trend.ChangeKg}");
    }

    [Fact]
    public async Task Objetivo_LoseFatActivo()
    {
        using var factory = new ApiFactoryDemo();
        await factory.SembrarCatalogosAsync();
        await factory.DemoAsync();
        var userId = await factory.DemoUserIdAsync();

        await using var context = factory.NuevoContexto();
        var activo = await context.Goals.SingleAsync(g => g.UserId == userId && g.IsActive);
        Assert.Equal(GoalType.LoseFat, activo.GoalType);
        Assert.True(activo.TargetCalories > 0);
    }

    [Fact]
    public async Task Comidas_TodosLosDias_ConLosAlimentosDelSeed_YMacrosDeFoods()
    {
        using var factory = new ApiFactoryDemo();
        await factory.SembrarCatalogosAsync();
        await factory.DemoAsync();
        var userId = await factory.DemoUserIdAsync();

        await using var context = factory.NuevoContexto();
        var comidas = await context.MealEntries.Include(m => m.Food).Where(m => m.UserId == userId).ToListAsync();
        Assert.Equal(Enumerable.Range(0, 14).Select(i => ApiFactoryDemo.Hoy.AddDays(i - 13)), comidas.Select(m => m.Date).Distinct().Order());
        foreach (var nombre in AlimentosDeLaDemo) Assert.Contains(comidas, m => m.Food?.Name == nombre);
        // D6: los macros congelados salen de Foods (cantidad × valor por 100 g).
        foreach (var m in comidas.Where(m => m.RecipeId is null))
        {
            Assert.Contains(m.Food!.Name, AlimentosDeLaDemo);
            Assert.Equal(Math.Round(m.Food.CaloriesPer100g * m.QuantityGrams / 100m, 2), Math.Round(m.SavedCalories, 2));
        }
        // Hoy quedan comidas por cargar: así "cuánto me falta" tiene sentido en la demo.
        Assert.True(comidas.Count(m => m.Date == ApiFactoryDemo.Hoy) < comidas.Count(m => m.Date == ApiFactoryDemo.Hoy.AddDays(-1)));
    }

    [Fact]
    public async Task Entrenamientos_2o3_ConRutinasPrecargadas()
    {
        using var factory = new ApiFactoryDemo();
        await factory.SembrarCatalogosAsync();
        await factory.DemoAsync();
        var userId = await factory.DemoUserIdAsync();

        await using var context = factory.NuevoContexto();
        var entrenamientos = await context.WorkoutLogs.Include(w => w.Sets).Where(w => w.UserId == userId).ToListAsync();
        Assert.InRange(entrenamientos.Count, 2, 3);
        var diasPrecargados = await context.RoutineDays.Where(d => context.Routines.Any(r => r.Id == d.RoutineId && r.UserId == null)).Select(d => d.Id).ToListAsync();
        Assert.All(entrenamientos, w =>
        {
            Assert.Contains(w.RoutineDayId!.Value, diasPrecargados);
            Assert.NotEmpty(w.Sets);
            Assert.True(w.EstimatedCaloriesBurned > 0);
            Assert.InRange(w.Date, ApiFactoryDemo.Hoy.AddDays(-13), ApiFactoryDemo.Hoy);
        });
    }

    [Fact]
    public async Task RecetaYFavorito()
    {
        using var factory = new ApiFactoryDemo();
        await factory.SembrarCatalogosAsync();
        await factory.DemoAsync();
        var userId = await factory.DemoUserIdAsync();

        await using var context = factory.NuevoContexto();
        var receta = await context.Recipes.Include(r => r.Ingredients).SingleAsync(r => r.UserId == userId);
        Assert.True(receta.Ingredients.Count >= 2);
        Assert.Single(await context.FavoriteFoods.Where(f => f.UserId == userId).ToListAsync());
        Assert.Contains(await context.MealEntries.Where(m => m.UserId == userId).ToListAsync(), m => m.RecipeId == receta.Id);
    }

    [Fact]
    public async Task Idempotente_LaSegundaCorridaNoAgregaNada()
    {
        using var factory = new ApiFactoryDemo();
        await factory.SembrarCatalogosAsync();
        var primera = await factory.DemoAsync();

        var segunda = await factory.DemoAsync();

        Assert.True(primera.UserCreated && primera.GoalCreated && primera.RecipeCreated);
        Assert.Equal(14, primera.Metrics);
        Assert.Equal(new DemoSeedSummary(false, 0, 0, 0, false, false), segunda);
        await using var context = factory.NuevoContexto();
        var userId = await factory.DemoUserIdAsync();
        Assert.Equal(1, await context.Users.CountAsync(u => u.Email == DemoDataService.DemoEmail));
        Assert.Equal(14, await context.BodyMetrics.CountAsync(m => m.UserId == userId));
        Assert.Equal(primera.Meals, await context.MealEntries.CountAsync(m => m.UserId == userId));
        Assert.Equal(primera.Workouts, await context.WorkoutLogs.CountAsync(w => w.UserId == userId));
        Assert.Equal(1, await context.Goals.CountAsync(g => g.UserId == userId));
        Assert.Equal(1, await context.Recipes.CountAsync(r => r.UserId == userId));
    }
}
