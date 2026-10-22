using AppNutricion.Entities.Entities;

namespace AppNutricion.DataAccess.Interfaces;

// "Account" para no confundirlo con IUserStore<TUser> de ASP.NET Core Identity.
// El email llega ya normalizado (minúsculas, sin espacios): el índice único de Users distingue mayúsculas.
public interface IUserAccountStore
{
    Task<User?> GetByEmailAsync(string normalizedEmail);
    Task<User?> GetByIdAsync(int id);

    // false si el email ya existe (violación del índice único IX_Users_Email).
    Task<bool> TryAddAsync(User user);
}
