namespace AppNutricion.BusinessLogic.Validators;

public static class TimeZones
{
    // Solo ids IANA ("America/Argentina/Cordoba", "UTC"). Un id de Windows ("Argentina Standard Time")
    // también se encuentra en Windows, pero no es IANA: se rechaza.
    public static bool TryFindIana(string? timeZoneId, out TimeZoneInfo timeZone)
    {
        timeZone = TimeZoneInfo.Utc;
        if (string.IsNullOrWhiteSpace(timeZoneId) || !TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var found))
            return false;
        if (!found.HasIanaId && !TimeZoneInfo.TryConvertIanaIdToWindowsId(timeZoneId, out _))
            return false;
        timeZone = found;
        return true;
    }

    // El día de hoy en esa zona horaria (los días del diario y del peso son locales).
    public static DateOnly Today(TimeProvider clock, TimeZoneInfo timeZone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), timeZone).DateTime);
}
