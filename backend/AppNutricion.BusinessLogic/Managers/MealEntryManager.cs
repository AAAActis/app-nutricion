using System.Globalization;
using System.Text;
using AppNutricion.BusinessLogic.Exceptions;
using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;

namespace AppNutricion.BusinessLogic.Managers;

public class MealEntryManager(IMealEntryStore store, IFoodManager foodManager, IRecipeManager recipeManager, IUserToday userToday, TimeProvider timeProvider)
    : IMealEntryManager
{
    private const decimal MaxGrams = 5000;
    private const int ItemNameMaxLength = 200;
    private const int MaxExportDays = 366;
    private const decimal MaxRecipeServings = 20;

    public async Task<MealEntry?> CreateAsync(int userId, int foodId, decimal quantity, bool isUnits, MealType mealType, DateOnly date, int? recipeId)
    {
        // Contrato del frontend sin cambios: una receta se carga por su endpoint, con porciones en vez de gramos.
        if (recipeId is not null) throw new ArgumentException("Para cargar una receta usá POST /api/meals/recipe.", nameof(recipeId));
        ValidateMealType(mealType);
        await userToday.ValidateDiaryDateAsync(userId, date, nameof(date));

        var food = await foodManager.GetByIdAsync(foodId, userId);
        if (food is null) return null;
        var grams = ValidGrams(foodManager.ConvertToGrams(food, quantity, isUnits));

        var entry = new MealEntry
        {
            UserId = userId,
            FoodId = food.Id,
            Date = date,
            MealType = mealType,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
        };
        Freeze(entry, food, grams);
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
            await userToday.ValidateDiaryDateAsync(userId, day, nameof(date));
            entry.Date = day;
        }
        if (isUnits is not null && quantity is null)
            throw new ArgumentException("Para cambiar entre gramos y unidades hay que mandar la cantidad.", nameof(isUnits));
        if (quantity is { } amount)
        {
            // Editar la cantidad es una acción nueva del usuario: "re-congela" los valores desde Foods (D19),
            // así no se arrastra el redondeo de lo guardado (1 g → 5000 g). Sin el alimento o la receta, proporcional.
            if (isUnits == true && entry.RecipeId is not null)
                throw new ArgumentException("Una comida de receta se edita en gramos.", nameof(isUnits));
            var food = entry.FoodId is { } foodId ? await foodManager.GetByIdAsync(foodId, userId) : null;
            var recipe = entry.RecipeId is { } recipeId ? await recipeManager.GetAsync(recipeId, userId) : null;
            if (isUnits == true && food is null)
                throw new ArgumentException("El alimento ya no existe: editá la cantidad en gramos.", nameof(quantity));
            var grams = ValidGrams(isUnits == true ? foodManager.ConvertToGrams(food!, amount, true) : amount);
            if (food is not null)
            {
                Freeze(entry, food, grams);
            }
            else if (recipe is not null)
            {
                Freeze(entry, recipe, grams, grams / recipe.TotalGrams);
            }
            else
            {
                var oldGrams = entry.QuantityGrams;
                entry.SavedCalories = Math.Round(entry.SavedCalories * grams / oldGrams, 2);
                entry.SavedProtein = Math.Round(entry.SavedProtein * grams / oldGrams, 2);
                entry.SavedCarbs = Math.Round(entry.SavedCarbs * grams / oldGrams, 2);
                entry.SavedFat = Math.Round(entry.SavedFat * grams / oldGrams, 2);
                entry.QuantityGrams = grams;
            }
        }

        return await store.UpdateAsync(entry, userId) ? entry : null;
    }

    public async Task<MealEntry?> CreateFromRecipeAsync(int userId, int recipeId, decimal servings, MealType mealType, DateOnly date)
    {
        if (servings is <= 0 or > MaxRecipeServings)
            throw new ArgumentException($"Las porciones tienen que ser más de 0 y hasta {MaxRecipeServings}.", nameof(servings));
        ValidateMealType(mealType);
        await userToday.ValidateDiaryDateAsync(userId, date, nameof(date));

        var recipe = await recipeManager.GetAsync(recipeId, userId);
        if (recipe is null) return null;
        var factor = servings / recipe.Recipe.Servings;
        var grams = Math.Round(recipe.TotalGrams * factor, 2);
        if (grams > MaxGrams)
            throw new ArgumentException($"Esas porciones pesan más de {MaxGrams} g: cargá menos.", nameof(servings));

        var entry = new MealEntry
        {
            UserId = userId,
            RecipeId = recipe.Recipe.Id,
            Date = date,
            MealType = mealType,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
        };
        Freeze(entry, recipe, grams, factor);
        await store.AddAsync([entry]);
        return entry;
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
        await userToday.ValidateDiaryDateAsync(userId, toDate, nameof(toDate));
        if (mealType is { } type) ValidateMealType(type);
        // Copiar dos veces duplicaría todo (D20): el destino no puede tener comidas del tipo pedido (o de ninguno, sin tipo).
        if ((await store.GetByDateAsync(userId, toDate, mealType)).Count > 0)
            throw new ConflictException("El día destino ya tiene comidas cargadas; borralas o copiá un tipo de comida que no tenga");

        var now = timeProvider.GetUtcNow().UtcDateTime;
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

    public async Task<string> ExportCsvAsync(int userId, DateOnly? from, DateOnly? to)
    {
        if (from is not { } start) throw new ArgumentException("Falta la fecha de inicio (from=YYYY-MM-DD).", nameof(from));
        if (to is not { } end) throw new ArgumentException("Falta la fecha de fin (to=YYYY-MM-DD).", nameof(to));
        if (end < start) throw new ArgumentException("La fecha de fin no puede ser anterior a la de inicio.", nameof(to));
        if (end.DayNumber - start.DayNumber + 1 > MaxExportDays)
            throw new ArgumentException($"El rango es de hasta {MaxExportDays} días.", nameof(to));

        var csv = new StringBuilder("fecha,tipo,alimento,gramos,kcal,proteinas_g,carbohidratos_g,grasas_g\r\n");
        foreach (var m in await store.GetRangeAsync(userId, start, end))
        {
            csv.Append(CultureInfo.InvariantCulture,
                $"{m.Date:yyyy-MM-dd},{m.MealType},{CsvText(m.ItemName)},{m.QuantityGrams},{m.SavedCalories},{m.SavedProtein},{m.SavedCarbs},{m.SavedFat}\r\n");
        }
        return csv.ToString();
    }

    // Un texto que empieza como fórmula (= + - @ tab) se neutraliza con ' para que Excel no lo ejecute;
    // después, escapado RFC 4180.
    private static string CsvText(string text)
    {
        if (text.Length > 0 && "=+-@\t".Contains(text[0])) text = "'" + text;
        return text.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? $"\"{text.Replace("\"", "\"\"")}\"" : text;
    }

    // Congela nombre, gramos y macros desde Foods (D6): después no se recalculan solos.
    private static void Freeze(MealEntry entry, Food food, decimal grams)
    {
        var name = string.IsNullOrWhiteSpace(food.Brand) ? food.Name : $"{food.Name} ({food.Brand})";
        entry.ItemName = name.Length > ItemNameMaxLength ? name[..ItemNameMaxLength] : name;
        entry.QuantityGrams = grams;
        entry.SavedCalories = Math.Round(grams * food.CaloriesPer100g / 100, 2);
        entry.SavedProtein = Math.Round(grams * food.ProteinPer100g / 100, 2);
        entry.SavedCarbs = Math.Round(grams * food.CarbsPer100g / 100, 2);
        entry.SavedFat = Math.Round(grams * food.FatPer100g / 100, 2);
    }

    // Igual desde una receta: sus totales salen de Foods (D6); factor = qué parte de la receta entera se comió.
    private static void Freeze(MealEntry entry, RecipeDetail recipe, decimal grams, decimal factor)
    {
        entry.ItemName = recipe.Recipe.Name;
        entry.QuantityGrams = grams;
        entry.SavedCalories = Math.Round(recipe.Totals.Calories * factor, 2);
        entry.SavedProtein = Math.Round(recipe.Totals.ProteinG * factor, 2);
        entry.SavedCarbs = Math.Round(recipe.Totals.CarbsG * factor, 2);
        entry.SavedFat = Math.Round(recipe.Totals.FatG * factor, 2);
    }

    private static decimal ValidGrams(decimal grams)
    {
        grams = Math.Round(grams, 2);
        if (grams <= 0 || grams > MaxGrams)
            throw new ArgumentException($"La cantidad tiene que ser mayor a 0 y de hasta {MaxGrams} g.", "quantity");
        return grams;
    }

    private static void ValidateMealType(MealType mealType)
    {
        if (!Enum.IsDefined(mealType)) throw new ArgumentException("Tipo de comida inválido.", nameof(mealType));
    }
}
