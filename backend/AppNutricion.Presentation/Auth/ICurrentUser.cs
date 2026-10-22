namespace AppNutricion.Presentation.Auth;

// El usuario del request, sacado del claim "sub" del token (D6: el userId nunca viene del cliente).
// Uso en un controller con [Authorize]:  var userId = currentUser.RequireUserId();
public interface ICurrentUser
{
    // null si no hay token válido o si "sub" no es un número.
    int? UserId { get; }

    // Para endpoints con [Authorize]: si no hay userId es un bug de configuración (500), no un 401.
    int RequireUserId();
}
