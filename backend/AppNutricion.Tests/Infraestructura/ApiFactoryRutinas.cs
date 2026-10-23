using AppNutricion.BusinessLogic.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace AppNutricion.Tests.Infraestructura;

// Como ApiFactoryAutenticada, para los controllers de rutinas y entrenamientos (S7): esquema "Test" y managers mockeados.
public class ApiFactoryRutinas : ApiFactory
{
    public Mock<IRoutineManager> RoutineManager { get; } = new();
    public Mock<IWorkoutManager> WorkoutManager { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(TestAuthHandler.Esquema)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.Esquema, _ => { });
            services.AddSingleton(RoutineManager.Object);
            services.AddSingleton(WorkoutManager.Object);
        });
    }

    public HttpClient CrearCliente(int? userId)
    {
        var client = CreateClient();
        if (userId is not null) client.DefaultRequestHeaders.Add(TestAuthHandler.Header, userId.ToString());
        return client;
    }
}
