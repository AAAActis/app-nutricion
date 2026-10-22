using AppNutricion.DataAccess.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AppNutricion.Tests.Infraestructura;

// La API completa (JwtBearer, controllers, pipeline) con los usuarios en memoria en vez de Postgres.
public class ApiFactoryAuth : ApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
            services.Replace(ServiceDescriptor.Singleton<IUserAccountStore, UserAccountStoreEnMemoria>()));
    }
}
