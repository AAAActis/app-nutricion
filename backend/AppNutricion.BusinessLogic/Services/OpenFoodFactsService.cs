using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;
using Microsoft.Extensions.Logging;

namespace AppNutricion.BusinessLogic.Services;

public class OpenFoodFactsService(HttpClient http, ILogger<OpenFoodFactsService> logger) : IOpenFoodFactsClient
{
    public static void Configure(HttpClient client) => client.BaseAddress = new Uri("https://world.openfoodfacts.org/");

    public async Task<Food?> GetByBarcodeAsync(string barcode)
    {
        var body = await http.GetFromJsonAsync<OffResponse>($"api/v3/product/{barcode}.json");
        var p = body!.Product!;
        return new Food
        {
            Name = p.ProductName!, Brand = p.Brands, Barcode = barcode, ExternalId = barcode, Source = FoodSource.OpenFoodFacts,
            CaloriesPer100g = p.Nutriments?.EnergyKcal ?? 0, ProteinPer100g = p.Nutriments?.Proteins ?? 0,
            CarbsPer100g = p.Nutriments?.Carbohydrates ?? 0, FatPer100g = p.Nutriments?.Fat ?? 0,
        };
    }

    private record OffResponse([property: JsonPropertyName("product")] OffProduct? Product);

    private record OffProduct(
        [property: JsonPropertyName("product_name")] string? ProductName,
        [property: JsonPropertyName("brands")] string? Brands,
        [property: JsonPropertyName("nutriments")] OffNutriments? Nutriments);

    private record OffNutriments(
        [property: JsonPropertyName("energy-kcal_100g")] decimal? EnergyKcal,
        [property: JsonPropertyName("proteins_100g")] decimal? Proteins,
        [property: JsonPropertyName("carbohydrates_100g")] decimal? Carbohydrates,
        [property: JsonPropertyName("fat_100g")] decimal? Fat);
}
