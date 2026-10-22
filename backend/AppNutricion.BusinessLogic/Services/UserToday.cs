using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.BusinessLogic.Validators;
using AppNutricion.DataAccess.Interfaces;

namespace AppNutricion.BusinessLogic.Services;

public class UserToday(IProfileStore profileStore, TimeProvider timeProvider) : IUserToday
{
    public async Task<DateOnly> GetAsync(int userId) => (await TodayAsync(userId)).Today;

    public async Task ValidateDiaryDateAsync(int userId, DateOnly date, string paramName)
    {
        var (today, hasZone) = await TodayAsync(userId);
        if (date > (hasZone ? today : today.AddDays(1)) || date < today.AddYears(-2))
            throw new ArgumentException("La fecha no puede ser futura ni de hace más de 2 años.", paramName);
    }

    private async Task<(DateOnly Today, bool HasZone)> TodayAsync(int userId)
    {
        var profile = await profileStore.GetByUserIdAsync(userId);
        var timeZone = TimeZoneInfo.Utc;
        var hasZone = profile is not null && TimeZones.TryFindIana(profile.TimeZoneId, out timeZone);
        return (TimeZones.Today(timeProvider, timeZone), hasZone);
    }
}
