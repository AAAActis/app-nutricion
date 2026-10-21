using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;
using Microsoft.Extensions.Logging;

namespace AppNutricion.BusinessLogic.Services;

// API v3 de Open Food Facts (D18). Solo lectura por código de barras.
public class OpenFoodFactsService(HttpClient http, ILogger<OpenFoodFactsService> logger) : IOpenFoodFactsClient
{
    private const decimal KjPerKcal = 4.184m;

    public static void Configure(HttpClient client)
    {
        client.BaseAddress = new Uri("https://world.openfoodfacts.org/");
        client.Timeout = TimeSpan.FromSeconds(5);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("AppNutricion/1.0 (PLA II UNC)");
    }

    public async Task<Food?> GetByBarcodeAsync(string barcode)
    {
        try
        {
            using var response = await http.GetAsync($"api/v3/product/{barcode}.json?fields=product_name,brands,nutriments");
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<OffResponse>();
            return body?.Product is { } product ? ToFood(barcode, product) : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "Open Food Facts no respondió bien para {Barcode}; se trata como no encontrado", barcode);
            return null;
        }
    }

    // shortcut: OFF da bebidas por 100 ml y acá se toman como 100 g; corregir si se suma densidad.
    private static Food? ToFood(string barcode, OffProduct product)
    {
        var n = product.Nutriments;
        var kcal = n?.EnergyKcal ?? n?.EnergyKj / KjPerKcal;
        if (kcal is null || n!.Proteins is not { } protein || n.Carbohydrates is not { } carbs || n.Fat is not { } fat) return null;
        return new Food
        {
            Name = product.ProductName?.Trim() ?? "",
            Brand = product.Brands?.Split(',')[0].Trim() is { Length: > 0 } brand ? brand : null,
            Barcode = barcode,
            Source = FoodSource.OpenFoodFacts,
            ExternalId = barcode,
            CaloriesPer100g = Math.Round(kcal.Value, 2),
            ProteinPer100g = Math.Round(protein, 2),
            CarbsPer100g = Math.Round(carbs, 2),
            FatPer100g = Math.Round(fat, 2),
        };
    }

    private record OffResponse([property: JsonPropertyName("product")] OffProduct? Product);

    private record OffProduct(
        [property: JsonPropertyName("product_name")] string? ProductName,
        [property: JsonPropertyName("brands")] string? Brands,
        [property: JsonPropertyName("nutriments")] OffNutriments? Nutriments);

    private record OffNutriments(
        [property: JsonPropertyName("energy-kcal_100g")] decimal? EnergyKcal,
        [property: JsonPropertyName("energy-kj_100g")] decimal? EnergyKj,
        [property: JsonPropertyName("proteins_100g")] decimal? Proteins,
        [property: JsonPropertyName("carbohydrates_100g")] decimal? Carbohydrates,
        [property: JsonPropertyName("fat_100g")] decimal? Fat);
}
