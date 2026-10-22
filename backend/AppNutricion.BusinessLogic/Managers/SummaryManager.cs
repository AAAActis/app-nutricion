using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.DataAccess.Interfaces;

namespace AppNutricion.BusinessLogic.Managers;

public class SummaryManager(IMealEntryStore store, IGoalManager goalManager, IUserToday userToday) : ISummaryManager
{
    public Task<DailySummary> GetDailyAsync(int userId, DateOnly? date) => throw new NotImplementedException();
}
