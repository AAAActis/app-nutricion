using AppNutricion.Entities.Entities;

namespace AppNutricion.Presentation.Output;

// Lo único que sale de un User. La entidad nunca se serializa: tiene el PasswordHash.
public record UserOutput(int Id, string Email)
{
    public static UserOutput From(User u) => new(u.Id, u.Email);
}
