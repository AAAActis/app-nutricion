using AppNutricion.Entities.Entities;

namespace AppNutricion.BusinessLogic.Interfaces;

// Enums como texto ("Male", "Moderate"): así un valor inválido es un 400 con su campo.
public record ProfileData(DateOnly? BirthDate, string? BiologicalSex, decimal? HeightCm, string? ActivityLevel, string? TimeZoneId);

public record SaveProfileResult(Profile Profile, bool Created);

public interface IProfileManager
{
    Task<Profile?> GetAsync(int userId);

    // Datos inválidos → ArgumentException con ParamName = birthDate / biologicalSex / heightCm / activityLevel / timeZoneId.
    Task<SaveProfileResult> SaveAsync(int userId, ProfileData data);
}
