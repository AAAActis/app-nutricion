using AppNutricion.DataAccess.DataBaseContext;
using AppNutricion.Entities.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AppNutricion.Tests.Infraestructura;

// Base SQLite en memoria para probar los Stores (filtros por usuario, orden, deduplicado) sin Postgres.
// No sirve para lo propio de Postgres (ILIKE, trigram): eso no se prueba acá.
public sealed class BaseSqlite : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public BaseSqlite(params int[] userIds)
    {
        _connection.Open();
        using var context = NuevoContexto();
        context.Database.EnsureCreated();
        context.Users.AddRange(userIds.Select(id => new User
        {
            Id = id, Email = $"u{id}@test", PasswordHash = "x", ConsentVersion = "1",
            ConsentAcceptedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow,
        }));
        context.SaveChanges();
    }

    // Un contexto nuevo por operación, como en la API (scoped por request): nada queda en el ChangeTracker.
    public ApplicationDbContext NuevoContexto() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options);

    public void Dispose() => _connection.Dispose();
}
