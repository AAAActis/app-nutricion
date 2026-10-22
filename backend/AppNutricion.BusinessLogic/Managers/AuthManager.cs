using System.Net.Mail;
using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using Microsoft.AspNetCore.Identity;

namespace AppNutricion.BusinessLogic.Managers;

public class AuthManager(IUserAccountStore userStore, IPasswordHasher<User> passwordHasher, IAccessTokenService tokenService) : IAuthManager
{
    private const int MaxEmailLength = 256;   // varchar(256) en Users.Email
    private const int MinPasswordLength = 8;
    private const int MaxPasswordLength = 128;

    public Task<RegisterResult> RegisterAsync(string? email, string? password, bool consentAccepted)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (!IsValidEmail(normalizedEmail))
            throw new ArgumentException("El email no es válido.", nameof(email));
        if (password is null || password.Length < MinPasswordLength || password.Length > MaxPasswordLength)
            throw new ArgumentException($"La contraseña debe tener entre {MinPasswordLength} y {MaxPasswordLength} caracteres.", nameof(password));
        if (!consentAccepted)
            throw new ArgumentException("Hay que aceptar el consentimiento para registrarse.", nameof(consentAccepted));

        return Task.FromResult(new RegisterResult(RegisterStatus.Registered, null));
    }

    public Task<LoginResult?> LoginAsync(string? email, string? password) =>
        Task.FromResult<LoginResult?>(null);

    // El índice único de Users.Email distingue mayúsculas: se guarda y se busca siempre así.
    private static string NormalizeEmail(string? email) => (email ?? "").Trim().ToLowerInvariant();

    // MailAddress acepta formatos como "Ana <ana@mail.com>": solo vale si la dirección es exactamente lo escrito.
    private static bool IsValidEmail(string email) =>
        email.Length is > 0 and <= MaxEmailLength
        && MailAddress.TryCreate(email, out var address)
        && address.Address == email;
}
