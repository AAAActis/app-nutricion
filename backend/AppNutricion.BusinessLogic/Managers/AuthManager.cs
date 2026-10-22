using System.Net.Mail;
using AppNutricion.BusinessLogic.Constants;
using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using Microsoft.AspNetCore.Identity;

namespace AppNutricion.BusinessLogic.Managers;

public class AuthManager(IUserAccountStore userStore, IPasswordHasher<User> passwordHasher, IAccessTokenService tokenService, TimeProvider timeProvider) : IAuthManager
{
    private const int MaxEmailLength = 256;   // varchar(256) en Users.Email
    private const int MinPasswordLength = 8;
    private const int MaxPasswordLength = 128;

    // Hash (PasswordHasher v3) de una contraseña aleatoria que no se guardó en ningún lado.
    private const string DummyPasswordHash = "AQAAAAIAAYagAAAAEANfj9Z9jxn2mvOhLY9GFsV3FkQ8hn2lKOpEAhWCIwpqEqXl0M+JoUOvEpQ5o6LAbg==";
    private static readonly User DummyUser = new();

    public async Task<RegisterResult> RegisterAsync(string? email, string? password, bool consentAccepted)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (!IsValidEmail(normalizedEmail))
            throw new ArgumentException("El email no es válido.", nameof(email));
        if (password is null || password.Length < MinPasswordLength || password.Length > MaxPasswordLength)
            throw new ArgumentException($"La contraseña debe tener entre {MinPasswordLength} y {MaxPasswordLength} caracteres.", nameof(password));
        if (!consentAccepted)
            throw new ArgumentException("Hay que aceptar el consentimiento para registrarse.", nameof(consentAccepted));

        if (await userStore.GetByEmailAsync(normalizedEmail) is not null)
            return new RegisterResult(RegisterStatus.EmailAlreadyRegistered, null);

        var now = timeProvider.GetUtcNow().UtcDateTime;   // Npgsql solo acepta DateTime UTC en timestamptz
        var user = new User
        {
            Email = normalizedEmail,
            ConsentAcceptedAt = now,
            ConsentVersion = ConsentVersions.Current,
            CreatedAt = now,
        };
        user.PasswordHash = passwordHasher.HashPassword(user, password);

        // false = otro registro con el mismo email ganó la carrera entre el GetByEmail y el insert.
        if (!await userStore.TryAddAsync(user))
            return new RegisterResult(RegisterStatus.EmailAlreadyRegistered, null);
        return new RegisterResult(RegisterStatus.Registered, user);
    }

    public async Task<LoginResult?> LoginAsync(string? email, string? password)
    {
        var normalizedEmail = NormalizeEmail(email);
        // Una contraseña de más de 128 no pudo registrarse: se descarta sin hashear (evita DoS con contraseñas enormes).
        if (normalizedEmail.Length == 0 || string.IsNullOrEmpty(password) || password.Length > MaxPasswordLength) return null;

        var user = await userStore.GetByEmailAsync(normalizedEmail);
        // Si el email no existe se verifica igual contra un hash de relleno: así el tiempo de respuesta
        // no revela qué emails están registrados.
        var verification = passwordHasher.VerifyHashedPassword(user ?? DummyUser, user?.PasswordHash ?? DummyPasswordHash, password);
        if (user is null || verification == PasswordVerificationResult.Failed) return null;

        return tokenService.CreateFor(user);
    }

    public Task<User?> GetUserAsync(int userId) => userStore.GetByIdAsync(userId);

    // El índice único de Users.Email distingue mayúsculas: se guarda y se busca siempre así.
    private static string NormalizeEmail(string? email) => (email ?? "").Trim().ToLowerInvariant();

    // MailAddress acepta formatos como "Ana <ana@mail.com>": solo vale si la dirección es exactamente lo escrito.
    private static bool IsValidEmail(string email) =>
        email.Length is > 0 and <= MaxEmailLength
        && MailAddress.TryCreate(email, out var address)
        && address.Address == email;
}
