using AppNutricion.Entities.Entities;

namespace AppNutricion.BusinessLogic.Interfaces;

public enum RegisterStatus { Registered, EmailAlreadyRegistered }

public record RegisterResult(RegisterStatus Status, User? User);

// Pensado para sumar el refresh token en J3.
public record LoginResult(string AccessToken, DateTime ExpiresAt);

public interface IAuthManager
{
    // Datos inválidos → ArgumentException con ParamName = email / password / consentAccepted.
    Task<RegisterResult> RegisterAsync(string? email, string? password, bool consentAccepted);

    // null = credenciales inválidas (mismo resultado si el email no existe o la contraseña no coincide).
    Task<LoginResult?> LoginAsync(string? email, string? password);

    // El usuario del token (para /api/me). null si ya no existe.
    Task<User?> GetUserAsync(int userId);
}
