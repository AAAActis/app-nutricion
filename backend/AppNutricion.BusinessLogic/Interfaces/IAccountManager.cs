using AppNutricion.DataAccess.Interfaces;

namespace AppNutricion.BusinessLogic.Interfaces;

// La cuenta del usuario logueado (J5): borrado y exportación de datos.
public interface IAccountManager
{
    // false = la contraseña no coincide (o la cuenta ya no existe): no se borra nada.
    Task<bool> DeleteAsync(int userId, string? password);

    // null = la cuenta no existe.
    Task<UserDataExport?> ExportAsync(int userId);
}
