using System.Collections.Concurrent;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;

namespace AppNutricion.Tests.Infraestructura;

// Imita a Users en Postgres: Id autoincremental y email único que distingue mayúsculas
// (igual que IX_Users_Email). Así se prueba que la normalización la hace el manager, no el store.
public sealed class UserAccountStoreEnMemoria : IUserAccountStore
{
    private readonly ConcurrentDictionary<string, User> _porEmail = new(StringComparer.Ordinal);
    private int _ultimoId;

    public Task<User?> GetByEmailAsync(string normalizedEmail) =>
        Task.FromResult(_porEmail.GetValueOrDefault(normalizedEmail));

    public Task<User?> GetByIdAsync(int id) =>
        Task.FromResult(_porEmail.Values.FirstOrDefault(u => u.Id == id));

    public Task<bool> TryAddAsync(User user)
    {
        if (!_porEmail.TryAdd(user.Email, user)) return Task.FromResult(false);
        user.Id = Interlocked.Increment(ref _ultimoId);
        return Task.FromResult(true);
    }

    public Task DeleteWithDataAsync(int userId)
    {
        foreach (var (email, _) in _porEmail.Where(p => p.Value.Id == userId).ToList())
            _porEmail.TryRemove(email, out _);
        return Task.CompletedTask;
    }

    // Para los tests que arman el token a mano (UsuarioDePrueba): el usuario del "sub" tiene que existir.
    public void Agregar(int id) =>
        _porEmail[$"u{id}@test"] = new User { Id = id, Email = $"u{id}@test", PasswordHash = "x", ConsentVersion = "1" };
}
