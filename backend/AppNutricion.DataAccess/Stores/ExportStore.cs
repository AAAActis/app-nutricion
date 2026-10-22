using AppNutricion.DataAccess.Interfaces;

namespace AppNutricion.DataAccess.Stores;

public class ExportStore : IExportStore
{
    public Task<UserDataExport?> GetAsync(int userId) => Task.FromResult<UserDataExport?>(null);
}
