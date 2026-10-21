using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AppNutricion.Tests.Infraestructura;

// Levanta la API en memoria sin depender de Postgres ni de los user-secrets de quien corre los tests.
// Program.cs corta el arranque si falta la cadena de conexión, así que se pasa una que nunca se usa.
public class ApiFactory : WebApplicationFactory<Program>
{
    public const string CadenaDeConexionFalsa = "Host=localhost;Database=no-se-usa-en-tests";

    protected virtual string Entorno => "Testing";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Entorno);
        builder.UseSetting("ConnectionStrings:DefaultConnection", CadenaDeConexionFalsa);
    }
}

// Para lo que solo existe en Development (Swagger UI, documento OpenAPI).
public class ApiFactoryDesarrollo : ApiFactory
{
    protected override string Entorno => "Development";
}
