using AppNutricion.Entities.Enums;

namespace AppNutricion.Entities.Entities;

public class Profile
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateOnly BirthDate { get; set; }                 // la edad se calcula, no se guarda
    public BiologicalSex BiologicalSex { get; set; }
    public decimal HeightCm { get; set; }
    public ActivityLevel ActivityLevel { get; set; }
    public string TimeZoneId { get; set; } = null!;         // IANA, ej. "America/Argentina/Cordoba"
    public DateTime UpdatedAt { get; set; }
}
