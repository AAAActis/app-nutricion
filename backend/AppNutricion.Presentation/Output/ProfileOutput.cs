using AppNutricion.Entities.Entities;

namespace AppNutricion.Presentation.Output;

public record ProfileOutput(DateOnly BirthDate, string BiologicalSex, decimal HeightCm, string ActivityLevel, string TimeZoneId, DateTime UpdatedAt)
{
    public static ProfileOutput From(Profile p) =>
        new(p.BirthDate, p.BiologicalSex.ToString(), p.HeightCm, p.ActivityLevel.ToString(), p.TimeZoneId, p.UpdatedAt);
}
