using AppNutricion.Entities.Entities;

namespace AppNutricion.BusinessLogic.Interfaces;

public interface IAccessTokenService
{
    // JWT firmado con el userId en el claim "sub". ExpiresAt en UTC.
    LoginResult CreateFor(User user);
}
