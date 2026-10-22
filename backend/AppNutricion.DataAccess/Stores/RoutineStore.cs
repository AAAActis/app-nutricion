using AppNutricion.DataAccess.DataBaseContext;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;

namespace AppNutricion.DataAccess.Stores;

public class RoutineStore(ApplicationDbContext context) : IRoutineStore
{
    public Task<List<Routine>> ListVisibleAsync(int userId) => throw new NotImplementedException();

    public Task<Routine?> GetVisibleAsync(int id, int userId) => throw new NotImplementedException();

    public Task<bool> IsDayVisibleAsync(int routineDayId, int userId) => throw new NotImplementedException();

    public Task AddAsync(Routine routine) => throw new NotImplementedException();

    public Task<bool> DeleteOwnAsync(int id, int userId) => throw new NotImplementedException();

    public Task<int> AddMissingPresetsAsync(IEnumerable<Routine> routines) => throw new NotImplementedException();
}
