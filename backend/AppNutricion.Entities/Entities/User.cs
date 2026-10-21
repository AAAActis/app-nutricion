namespace AppNutricion.Entities.Entities;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = null!;              // en minúsculas
    public string PasswordHash { get; set; } = null!;
    public DateTime ConsentAcceptedAt { get; set; }         // Ley 25.326
    public string ConsentVersion { get; set; } = null!;
    public DateTime CreatedAt { get; set; }

    public Profile? Profile { get; set; }
}
