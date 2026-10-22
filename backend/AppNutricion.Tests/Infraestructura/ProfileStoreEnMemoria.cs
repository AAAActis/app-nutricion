using System.Collections.Concurrent;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;

namespace AppNutricion.Tests.Infraestructura;

// Imita a Profiles: uno por usuario.
public sealed class ProfileStoreEnMemoria : IProfileStore
{
    private readonly ConcurrentDictionary<int, Profile> _porUsuario = new();
    private int _ultimoId;

    public Task<Profile?> GetByUserIdAsync(int userId) =>
        Task.FromResult(_porUsuario.TryGetValue(userId, out var p) ? Copia(p) : null);

    public Task<bool> UpsertAsync(Profile profile)
    {
        var creado = !_porUsuario.ContainsKey(profile.UserId);
        var guardado = Copia(profile);
        guardado.Id = creado ? Interlocked.Increment(ref _ultimoId) : _porUsuario[profile.UserId].Id;
        _porUsuario[profile.UserId] = guardado;
        profile.Id = guardado.Id;
        return Task.FromResult(creado);
    }

    // Se guarda y se devuelve una copia: como la base, no comparte la instancia con quien llama.
    private static Profile Copia(Profile p) => new()
    {
        Id = p.Id, UserId = p.UserId, BirthDate = p.BirthDate, BiologicalSex = p.BiologicalSex,
        HeightCm = p.HeightCm, ActivityLevel = p.ActivityLevel, TimeZoneId = p.TimeZoneId, UpdatedAt = p.UpdatedAt,
    };
}
