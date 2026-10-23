using AppNutricion.DataAccess.DataBaseContext;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace AppNutricion.Tests.Infraestructura;

// La API completa (JwtBearer, controllers, managers y stores reales) sobre SQLite en memoria en vez de Postgres.
// Como BaseSqlite: no sirve para lo propio de Postgres (búsqueda con ILIKE ni el error de la FK NoAction del borrado).
public class ApiFactorySqlite : ApiFactory
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        _connection.Open();
        builder.ConfigureTestServices(services =>
        {
            // Sacar la configuración de Npgsql: si no, el contexto queda con dos proveedores.
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(_connection));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated();
        return host;
    }

    // Con la base de la API, para mirar lo que quedó sin pasar por los endpoints.
    public ApplicationDbContext NuevoContexto() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options);

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}
