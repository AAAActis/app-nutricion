using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;

namespace AppNutricion.BusinessLogic.Managers;

public class MealEntryManager(IMealEntryStore store, IFoodManager foodManager) : IMealEntryManager
{
    private const decimal MaxGrams = 5000;
    private const int ItemNameMaxLength = 200;

    public async Task<MealEntry?> CreateAsync(int userId, int foodId, decimal quantity, bool isUnits, MealType mealType, DateOnly date, int? recipeId)
    {
        if (recipeId is not null) throw new ArgumentException("Las recetas llegan en L9: por ahora solo se cargan alimentos.", nameof(recipeId));
        ValidateMealType(mealType);
        ValidateDate(date, nameof(date));

        var food = await foodManager.GetByIdAsync(foodId, userId);
        if (food is null) return null;
        var grams = ValidGrams(foodManager.ConvertToGrams(food, quantity, isUnits));

        var name = string.IsNullOrWhiteSpace(food.Brand) ? food.Name : $"{food.Name} ({food.Brand})";
        var entry = new MealEntry
        {
            UserId = userId,
            FoodId = food.Id,
            ItemName = name.Length > ItemNameMaxLength ? name[..ItemNameMaxLength] : name,
            Date = date,
            MealType = mealType,
            QuantityGrams = grams,
            SavedCalories = Math.Round(grams * food.CaloriesPer100g / 100, 2),
            SavedProtein = Math.Round(grams * food.ProteinPer100g / 100, 2),
            SavedCarbs = Math.Round(grams * food.CarbsPer100g / 100, 2),
            SavedFat = Math.Round(grams * food.FatPer100g / 100, 2),
            CreatedAt = DateTime.UtcNow,
        };
        await store.AddAsync([entry]);
        return entry;
    }

    public async Task<MealEntry?> UpdateAsync(int id, int userId, decimal? quantity, bool? isUnits, MealType? mealType, DateOnly? date)
    {
        var entry = await store.GetByIdAsync(id, userId);
        if (entry is null) return null;

        if (mealType is { } type)
        {
            ValidateMealType(type);
            entry.MealType = type;
        }
        if (date is { } day)
        {
            ValidateDate(day, nameof(date));
            entry.Date = day;
        }
        if (isUnits is not null && quantity is null)
            throw new ArgumentException("Para cambiar entre gramos y unidades hay que mandar la cantidad.", nameof(isUnits));
        if (quantity is { } amount)
        {
            var grams = ValidGrams(isUnits == true ? await UnitsToGramsAsync(entry, amount, userId) : amount);
            // Proporcional a lo guardado: los macros congelados nunca se vuelven a leer de Foods.
            var oldGrams = entry.QuantityGrams;
            entry.SavedCalories = Math.Round(entry.SavedCalories * grams / oldGrams, 2);
            entry.SavedProtein = Math.Round(entry.SavedProtein * grams / oldGrams, 2);
            entry.SavedCarbs = Math.Round(entry.SavedCarbs * grams / oldGrams, 2);
            entry.SavedFat = Math.Round(entry.SavedFat * grams / oldGrams, 2);
            entry.QuantityGrams = grams;
        }

        return await store.UpdateAsync(entry, userId) ? entry : null;
    }

    public Task<bool> DeleteAsync(int id, int userId) => store.DeleteAsync(id, userId);

    public Task<List<MealEntry>> ListAsync(int userId, DateOnly? date)
    {
        if (date is not { } day) throw new ArgumentException("Falta la fecha (date=YYYY-MM-DD).", nameof(date));
        return store.GetByDateAsync(userId, day, null);
    }

    public async Task<List<MealEntry>> CopyAsync(int userId, DateOnly fromDate, DateOnly toDate, MealType? mealType)
    {
        if (fromDate == toDate) throw new ArgumentException("El día de origen y el de destino son el mismo.", nameof(toDate));
        ValidateDate(toDate, nameof(toDate));
        if (mealType is { } type) ValidateMealType(type);

        var now = DateTime.UtcNow;
        var copies = (await store.GetByDateAsync(userId, fromDate, mealType)).Select(m => new MealEntry
        {
            UserId = userId,
            FoodId = m.FoodId,
            RecipeId = m.RecipeId,
            ItemName = m.ItemName,
            Date = toDate,
            MealType = m.MealType,
            QuantityGrams = m.QuantityGrams,
            SavedCalories = m.SavedCalories,
            SavedProtein = m.SavedProtein,
            SavedCarbs = m.SavedCarbs,
            SavedFat = m.SavedFat,
            CreatedAt = now,
        }).ToList();
        if (copies.Count > 0) await store.AddAsync(copies);
        return copies;
    }

    // Solo para pasar unidades a gramos: la porción sale del alimento, los macros no.
    private async Task<decimal> UnitsToGramsAsync(MealEntry entry, decimal quantity, int userId)
    {
        var food = entry.FoodId is { } foodId ? await foodManager.GetByIdAsync(foodId, userId) : null;
        if (food is null) throw new ArgumentException("El alimento ya no existe: editá la cantidad en gramos.", nameof(quantity));
        return foodManager.ConvertToGrams(food, quantity, true);
    }

    private static decimal ValidGrams(decimal grams)
    {
        grams = Math.Round(grams, 2);
        if (grams <= 0 || grams > MaxGrams)
            throw new ArgumentException($"La cantidad tiene que ser mayor a 0 y de hasta {MaxGrams} g.", "quantity");
        return grams;
    }

    // El cliente manda su día local: se acepta hasta mañana (UTC) por las zonas horarias adelantadas.
    private static void ValidateDate(DateOnly date, string paramName)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (date > today.AddDays(1) || date < today.AddYears(-2))
            throw new ArgumentException("La fecha tiene que estar entre hace 2 años y mañana.", paramName);
    }

    private static void ValidateMealType(MealType mealType)
    {
        if (!Enum.IsDefined(mealType)) throw new ArgumentException("Tipo de comida inválido.", nameof(mealType));
    }
}
