using AppNutricion.BusinessLogic.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AppNutricion.Tests.Infraestructura;

// Levanta la API en memoria sin depender de Postgres ni de los user-secrets de quien corre los tests.
// Program.cs corta el arranque si falta la cadena de conexión, así que se pasa una que nunca se usa.
public class ApiFactory : WebApplicationFactory<Program>
{
    public const string CadenaDeConexionFalsa = "Host=localhost;Database=no-se-usa-en-tests";
    // 32 bytes o más: el mínimo que exige el arranque para HMAC-SHA256. Solo para tests.
    public const string ClaveJwtFalsa = "clave-falsa-de-tests-con-32-bytes-o-mas!";

    protected virtual string Entorno => "Testing";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Entorno);
        builder.UseSetting("ConnectionStrings:DefaultConnection", CadenaDeConexionFalsa);
        builder.UseSetting("Jwt:Key", ClaveJwtFalsa);
        builder.ConfigureTestServices(services =>
        {
            // El seed de alimentos escribe en la base al arrancar (si existe foods_seed.json).
            var seed = services.Where(d => d.ServiceType == typeof(IHostedService)
                                           && (d.ImplementationType == typeof(FoodSeedHostedService) || d.ImplementationType == typeof(TrainingSeedHostedService))).ToList();
            foreach (var descriptor in seed)
            {
                services.Remove(descriptor);
            }
        });
    }
}

// Para lo que solo existe en Development (Swagger UI, documento OpenAPI).
public class ApiFactoryDesarrollo : ApiFactory
{
    protected override string Entorno => "Development";
}
