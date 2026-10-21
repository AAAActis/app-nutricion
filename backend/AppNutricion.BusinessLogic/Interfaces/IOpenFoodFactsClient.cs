using AppNutricion.Entities.Entities;

namespace AppNutricion.BusinessLogic.Interfaces;

public interface IOpenFoodFactsClient
{
    // null si no existe, si le faltan energía o macros por 100 g, o si OFF falla (timeout, 5xx, sin red).
    Task<Food?> GetByBarcodeAsync(string barcode);
}
