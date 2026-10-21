using System.Text.Json;
using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.BusinessLogic.Validators;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;
using AppNutricion.Entities.Helpers;
using Microsoft.Extensions.Logging;

namespace AppNutricion.BusinessLogic.Services;

public class FoodSeedService(IFoodStore foodStore, ILogger<FoodSeedService> logger) : IFoodSeedService
{
    private record SeedItem(
        string? Name, string? Brand, string? Barcode, string? Source, string? ExternalId,
        decimal CaloriesPer100g, decimal ProteinPer100g, decimal CarbsPer100g, decimal FatPer100g,
        string? PortionName, decimal? PortionWeightGrams);

    public async Task<FoodSeedSummary> SeedAsync(string json)
    {
        var items = JsonSerializer.Deserialize<List<SeedItem>>(json, JsonSerializerOptions.Web) ?? [];
        var valid = new List<Food>();
        var seen = new HashSet<(FoodSource, string)>();
        var discarded = 0;

        foreach (var item in items)
        {
            var food = ToFood(item);
            var reason = food is null ? "source faltante o desconocido" : FoodValidator.GetInvalidReason(food);
            if (reason is null && !seen.Add((food!.Source, food.ExternalId!))) reason = "(source, externalId) repetido en el archivo";
            if (reason is not null)
            {
                logger.LogWarning("foods_seed: se descarta {ExternalId} ({Name}): {Reason}", item.ExternalId, item.Name, reason);
                discarded++;
                continue;
            }
            valid.Add(food!);
        }

        var (inserted, updated) = valid.Count == 0 ? (0, 0) : await foodStore.UpsertSeedAsync(valid);
        return new FoodSeedSummary(inserted, updated, discarded);
    }

    private static Food? ToFood(SeedItem item)
    {
        FoodSource? source = item.Source switch
        {
            "Argenfoods" => FoodSource.Argenfoods,
            "OpenFoodFacts" => FoodSource.OpenFoodFacts,
            "Custom" => FoodSource.Custom,
            _ => null,
        };
        if (source is null) return null;
        var name = item.Name?.Trim() ?? "";
        return new Food
        {
            Name = name,
            NormalizedName = TextNormalizer.Normalize(name),
            Brand = item.Brand,
            Barcode = item.Barcode,
            Source = source.Value,
            ExternalId = item.ExternalId,
            // numeric(9,2): se redondea acá para que el upsert no lo vea como cambiado en cada arranque.
            CaloriesPer100g = Math.Round(item.CaloriesPer100g, 2),
            ProteinPer100g = Math.Round(item.ProteinPer100g, 2),
            CarbsPer100g = Math.Round(item.CarbsPer100g, 2),
            FatPer100g = Math.Round(item.FatPer100g, 2),
            PortionName = item.PortionName,
            PortionWeightGrams = item.PortionWeightGrams is { } p ? Math.Round(p, 2) : null,
            CreatedAt = DateTime.UtcNow,
        };
    }
}
