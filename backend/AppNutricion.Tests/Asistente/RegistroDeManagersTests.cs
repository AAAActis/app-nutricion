using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.BusinessLogic.Managers;
using AppNutricion.Tests.Infraestructura;
using Microsoft.Extensions.DependencyInjection;

namespace AppNutricion.Tests.Asistente;

// Con la API tal como la arma Program.cs (sin stores reemplazados): si falta el registro de un store, resolver el
// manager falla. Así se perdió IAssistantStore en el merge ac70b15 del #17.
public class RegistroDeManagersTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public void AsistenteRutinasYEntrenamientos_SeResuelvenDesdeUnScope()
    {
        using var scope = factory.Services.CreateScope();

        Assert.IsType<AssistantManager>(scope.ServiceProvider.GetRequiredService<IAssistantManager>());
        Assert.IsType<RoutineManager>(scope.ServiceProvider.GetRequiredService<IRoutineManager>());
        Assert.IsType<WorkoutManager>(scope.ServiceProvider.GetRequiredService<IWorkoutManager>());
    }
}
