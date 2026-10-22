using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.DataAccess.Interfaces;

namespace AppNutricion.BusinessLogic.Managers;

public class AccountManager : IAccountManager
{
    public Task<bool> DeleteAsync(int userId, string? password) => Task.FromResult(false);

    public Task<UserDataExport?> ExportAsync(int userId) => Task.FromResult<UserDataExport?>(null);
}
