using AppNutricion.BusinessLogic.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace AppNutricion.Tests.Infraestructura;

// Para los controllers con [Authorize]: esquema "Test" por defecto y managers mockeados.
// Los mocks son de la fixture (uno por clase de tests): cada clase los resetea en su constructor.
public class ApiFactoryAutenticada : ApiFactory
{
    public Mock<IFoodManager> FoodManager { get; } = new();
    public Mock<IMealEntryManager> MealManager { get; } = new();
    public Mock<ISummaryManager> SummaryManager { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(TestAuthHandler.Esquema)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.Esquema, _ => { });
            services.AddSingleton(FoodManager.Object);
            services.AddSingleton(MealManager.Object);
            services.AddSingleton(SummaryManager.Object);
        });
    }

    public HttpClient CrearCliente(int? userId)
    {
        var client = CreateClient();
        if (userId is not null) client.DefaultRequestHeaders.Add(TestAuthHandler.Header, userId.ToString());
        return client;
    }
}
