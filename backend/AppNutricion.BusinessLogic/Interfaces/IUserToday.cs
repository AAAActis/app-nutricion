namespace AppNutricion.BusinessLogic.Interfaces;

// "Hoy" del usuario: el día local de la zona horaria del perfil (sin perfil, UTC).
public interface IUserToday
{
    Task<DateOnly> GetAsync(int userId);

    // Rango de fechas del diario: ni futura ni de hace más de 2 años → si no, ArgumentException(paramName).
    // Sin perfil no se sabe la zona: hoy UTC + 1 de tolerancia por las zonas adelantadas.
    Task ValidateDiaryDateAsync(int userId, DateOnly date, string paramName);
}
