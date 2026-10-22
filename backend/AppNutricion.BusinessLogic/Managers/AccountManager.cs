using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using Microsoft.AspNetCore.Identity;

namespace AppNutricion.BusinessLogic.Managers;

public class AccountManager(IUserAccountStore userStore, IPasswordHasher<User> passwordHasher) : IAccountManager
{
    // Borrar la cuenta exige la contraseña actual: un token robado no alcanza.
    public async Task<bool> DeleteAsync(int userId, string? password)
    {
        // Igual que en el login: una contraseña de más del máximo no pudo registrarse y no se hashea.
        if (string.IsNullOrEmpty(password) || password.Length > AuthManager.MaxPasswordLength) return false;

        var user = await userStore.GetByIdAsync(userId);
        if (user is null || passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password) == PasswordVerificationResult.Failed)
            return false;

        await userStore.DeleteWithDataAsync(userId);
        return true;
    }

    public Task<UserDataExport?> ExportAsync(int userId) => Task.FromResult<UserDataExport?>(null);
}
