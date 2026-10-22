namespace AppNutricion.Presentation.Input;

// Todo opcional a propósito: las validaciones son del ProfileManager, que devuelve el campo que falla.
public record ProfileInput(DateOnly? BirthDate, string? BiologicalSex, decimal? HeightCm, string? ActivityLevel, string? TimeZoneId);
