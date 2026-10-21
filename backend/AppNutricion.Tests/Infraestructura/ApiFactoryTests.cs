using AppNutricion.BusinessLogic.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AppNutricion.Tests.Infraestructura;

public class ApiFactoryTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    // El seed escribe en la base al arrancar: los tests que levantan la app no pueden depender de Postgres.
    [Fact]
    public void ApiFactory_NoRegistraElSeedDeAlimentos()
    {
        var hostedServices = factory.Services.GetServices<IHostedService>();

        Assert.DoesNotContain(hostedServices, s => s is FoodSeedHostedService);
    }
}
