using AppNutricion.BusinessLogic.Exceptions;
using AppNutricion.BusinessLogic.Validators;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;

namespace AppNutricion.BusinessLogic.Managers;

// Peso y objetivos necesitan el perfil: la zona horaria define qué día es "hoy" y el motor usa sus datos.
internal static class ProfileGuard
{
    public const string MissingProfileMessage = "Primero completá el perfil.";

    public static async Task<(Profile Profile, DateOnly Today)> RequireAsync(IProfileStore profileStore, int userId, TimeProvider clock)
    {
        var profile = await profileStore.GetByUserIdAsync(userId) ?? throw new ConflictException(MissingProfileMessage);
        // La zona se validó al guardar el perfil; si igual no se encuentra, se usa UTC.
        TimeZones.TryFindIana(profile.TimeZoneId, out var timeZone);
        return (profile, TimeZones.Today(clock, timeZone));
    }
}
