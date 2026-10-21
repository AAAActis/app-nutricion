using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using Microsoft.AspNetCore.Identity;

namespace AppNutricion.BusinessLogic.Managers;

// C0 (contrato): sin lógica todavía. Se completa con TDD en C1 a C4.
public class AuthManager(IUserAccountStore userStore, IPasswordHasher<User> passwordHasher, IAccessTokenService tokenService) : IAuthManager
{
    public Task<RegisterResult> RegisterAsync(string? email, string? password, bool consentAccepted) =>
        Task.FromResult(new RegisterResult(RegisterStatus.Registered, null));

    public Task<LoginResult?> LoginAsync(string? email, string? password) =>
        Task.FromResult<LoginResult?>(null);
}
