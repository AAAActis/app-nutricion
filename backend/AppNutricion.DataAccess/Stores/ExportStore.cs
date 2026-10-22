using AppNutricion.DataAccess.DataBaseContext;
using AppNutricion.DataAccess.Interfaces;

namespace AppNutricion.DataAccess.Stores;

public class ExportStore(ApplicationDbContext context) : IExportStore
{
    // Stub: todavía no lee nada.
    public Task<UserDataExport?> GetAsync(int userId)
    {
        _ = context;
        return Task.FromResult<UserDataExport?>(null);
    }
}
