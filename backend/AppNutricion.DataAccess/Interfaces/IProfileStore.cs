using AppNutricion.Entities.Entities;

namespace AppNutricion.DataAccess.Interfaces;

public interface IProfileStore
{
    Task<Profile?> GetByUserIdAsync(int userId);

    // Perfil 1 a 1 con User: inserta si el usuario no tiene, si no actualiza sus campos. true = se creó.
    Task<bool> UpsertAsync(Profile profile);
}
