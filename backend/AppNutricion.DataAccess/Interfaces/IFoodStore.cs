using AppNutricion.Entities.Entities;

namespace AppNutricion.DataAccess.Interfaces;

// userId null = solo alimentos compartidos; con userId, también los personalizados de ese usuario.
public interface IFoodStore
{
    Task<List<Food>> SearchByNameAsync(string normalizedTerm, int? userId, int limit);
    Task<Food?> GetByIdAsync(int id, int? userId);
    Task<Food?> GetByBarcodeAsync(string barcode, int? userId);

    // Clave (Source, ExternalId). Inserta los nuevos, actualiza solo los que cambiaron, nunca toca los Custom.
    Task<(int Inserted, int Updated)> UpsertSeedAsync(IEnumerable<Food> foods);
}
