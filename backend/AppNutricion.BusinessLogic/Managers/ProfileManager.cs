using AppNutricion.BusinessLogic.Calculators;
using AppNutricion.BusinessLogic.Constants;
using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.BusinessLogic.Validators;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;

namespace AppNutricion.BusinessLogic.Managers;

public class ProfileManager(IProfileStore profileStore, TimeProvider timeProvider) : IProfileManager
{
    public Task<Profile?> GetAsync(int userId) => profileStore.GetByUserIdAsync(userId);

    public async Task<SaveProfileResult> SaveAsync(int userId, ProfileData data)
    {
        if (!TimeZones.TryFindIana(data.TimeZoneId, out var timeZone))
            throw new ArgumentException("La zona horaria tiene que ser un id IANA válido (por ejemplo, America/Argentina/Cordoba).", "timeZoneId");

        // La edad se cuenta con el día de hoy en la zona horaria del perfil.
        var age = data.BirthDate is { } birth ? NutritionCalculator.AgeAt(birth, TimeZones.Today(timeProvider, timeZone)) : -1;
        if (age < HealthDataLimits.MinAgeYears || age > HealthDataLimits.MaxAgeYears)
            throw new ArgumentException($"La edad tiene que estar entre {HealthDataLimits.MinAgeYears} y {HealthDataLimits.MaxAgeYears} años.", "birthDate");

        if (!EnumInput.TryParse<BiologicalSex>(data.BiologicalSex, out var sex))
            throw new ArgumentException($"El sexo biológico tiene que ser {string.Join(" o ", Enum.GetNames<BiologicalSex>())}.", "biologicalSex");

        if (data.HeightCm is not { } height || height < HealthDataLimits.MinHeightCm || height > HealthDataLimits.MaxHeightCm)
            throw new ArgumentException($"La altura tiene que estar entre {HealthDataLimits.MinHeightCm} y {HealthDataLimits.MaxHeightCm} cm.", "heightCm");

        if (!EnumInput.TryParse<ActivityLevel>(data.ActivityLevel, out var activity))
            throw new ArgumentException($"El nivel de actividad tiene que ser uno de: {string.Join(", ", Enum.GetNames<ActivityLevel>())}.", "activityLevel");

        var profile = new Profile
        {
            UserId = userId,
            BirthDate = data.BirthDate!.Value,
            BiologicalSex = sex,
            HeightCm = height,
            ActivityLevel = activity,
            TimeZoneId = data.TimeZoneId!,
            UpdatedAt = timeProvider.GetUtcNow().UtcDateTime,
        };
        var created = await profileStore.UpsertAsync(profile);
        return new SaveProfileResult(profile, created);
    }
}
