using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.BusinessLogic.Validators;
using AppNutricion.Entities.Enums;

namespace AppNutricion.BusinessLogic.Services;

// Una ingesta que el modelo propuso y pasó las validaciones. Se guarda como AssistantSuggestion Pending.
public record ProposedIntake(int FoodId, string FoodName, decimal QuantityGrams, MealType MealType, DateOnly Date);

// Result es el JSON que vuelve al modelo ({"error": "..."} si algo no valida).
public record ToolOutcome(string Result, ProposedIntake? Proposal);

// Las herramientas del asistente. Cada una delega en un manager (nunca en un store) y valida los argumentos del LLM
// como si vinieran de un cliente. El userId lo pone la API, nunca el modelo.
public class AssistantTools(IFoodManager foodManager, ISummaryManager summaryManager, IRoutineSummaryProvider routines, IUserToday userToday)
{
    public const string SearchFood = "buscar_alimento";
    public const string DailyTotals = "totales_del_dia";
    public const string ListRoutines = "consultar_rutinas";
    public const string ProposeIntake = "proponer_ingesta";
    private const int MaxSearchResults = 5;
    private const decimal MaxGrams = 5000;   // el mismo tope que MealEntryManager

    // Tildes legibles para el modelo: el JSON no se embebe en HTML.
    private static readonly JsonSerializerOptions Json = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public IReadOnlyList<ChatToolDefinition> Definitions { get; } =
    [
        new(SearchFood, "Busca alimentos en la base de la app. Devuelve hasta 5 con id, nombre, kcal y macros por 100 g.",
            """{"type":"object","properties":{"q":{"type":"string","description":"Nombre del alimento"}},"required":["q"]}"""),
        new(DailyTotals, "Totales consumidos en un día, el objetivo y lo que falta. Sin fecha, hoy.",
            """{"type":"object","properties":{"fecha":{"type":"string","description":"YYYY-MM-DD"}}}"""),
        new(ListRoutines, "Rutinas de entrenamiento disponibles para el usuario, con sus días.",
            """{"type":"object","properties":{}}"""),
        new(ProposeIntake, "Propone registrar una comida. No la registra: el usuario la confirma o la ignora. Usá un foodId de buscar_alimento.",
            """{"type":"object","properties":{"foodId":{"type":"integer"},"gramos":{"type":"number"},"mealType":{"type":"string","enum":["Breakfast","Lunch","AfternoonSnack","Dinner","Snack"]},"fecha":{"type":"string","description":"YYYY-MM-DD; sin fecha, hoy"}},"required":["foodId","gramos","mealType"]}"""),
    ];

    public async Task<ToolOutcome> ExecuteAsync(int userId, ChatToolCall call)
    {
        JsonElement args;
        try
        {
            args = JsonDocument.Parse(string.IsNullOrWhiteSpace(call.ArgumentsJson) ? "{}" : call.ArgumentsJson).RootElement;
        }
        catch (JsonException)
        {
            return Error("Los argumentos no son un JSON válido.");
        }
        if (args.ValueKind != JsonValueKind.Object) return Error("Los argumentos tienen que ser un objeto JSON.");

        return call.Name switch
        {
            SearchFood => await SearchAsync(userId, args),
            DailyTotals => await TotalsAsync(userId, args),
            ListRoutines => await RoutinesAsync(userId),
            ProposeIntake => await ProposeAsync(userId, args),
            _ => Error($"La herramienta {call.Name} no existe."),
        };
    }

    private async Task<ToolOutcome> SearchAsync(int userId, JsonElement args)
    {
        if (Text(args, "q") is not { } q) return Error("Falta q (el nombre del alimento).");
        var foods = await foodManager.SearchAsync(q, userId, MaxSearchResults);
        return Ok(new
        {
            resultados = foods.Take(MaxSearchResults).Select(f => new
            {
                id = f.Id, nombre = f.Name, marca = f.Brand, kcal = f.CaloriesPer100g, proteinas = f.ProteinPer100g,
                carbohidratos = f.CarbsPer100g, grasas = f.FatPer100g,
            }),
            nota = "Valores por 100 g.",
        });
    }

    private async Task<ToolOutcome> TotalsAsync(int userId, JsonElement args)
    {
        if (!TryDate(args, out var date)) return Error("fecha tiene que ser YYYY-MM-DD.");
        try
        {
            var day = await summaryManager.GetDailyAsync(userId, date);
            return Ok(new
            {
                fecha = day.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                consumido = Macros(day.Consumed),
                objetivo = day.Targets is null ? null : Macros(day.Targets),
                restante = day.Remaining is null ? null : new
                {
                    kcal = day.Remaining.Calories.Value, proteinas = day.Remaining.ProteinG.Value,
                    carbohidratos = day.Remaining.CarbsG.Value, grasas = day.Remaining.FatG.Value,
                },
                nota = "Estimación.",
            });
        }
        catch (ArgumentException ex)
        {
            return Error(Message(ex));
        }
    }

    private async Task<ToolOutcome> RoutinesAsync(int userId) =>
        Ok(new { rutinas = (await routines.GetAsync(userId)).Select(r => new { nombre = r.Name }) });

    private async Task<ToolOutcome> ProposeAsync(int userId, JsonElement args)
    {
        if (!args.TryGetProperty("foodId", out var idElement) || idElement.ValueKind != JsonValueKind.Number || !idElement.TryGetInt32(out var foodId))
            return Error("foodId tiene que ser un número entero (el id que devolvió buscar_alimento).");
        if (!args.TryGetProperty("gramos", out var gramsElement) || gramsElement.ValueKind != JsonValueKind.Number
            || !gramsElement.TryGetDecimal(out var grams) || grams <= 0 || grams > MaxGrams)
            return Error($"gramos tiene que ser mayor a 0 y de hasta {MaxGrams}.");
        if (!EnumInput.TryParse<MealType>(Text(args, "mealType"), out var mealType))
            return Error($"mealType tiene que ser uno de: {string.Join(", ", Enum.GetNames<MealType>())}.");
        if (!TryDate(args, out var requested)) return Error("fecha tiene que ser YYYY-MM-DD.");
        var date = requested ?? await userToday.GetAsync(userId);
        try
        {
            await userToday.ValidateDiaryDateAsync(userId, date, "fecha");
        }
        catch (ArgumentException ex)
        {
            return Error(Message(ex));
        }

        var food = await foodManager.GetByIdAsync(foodId, userId);
        if (food is null) return Error("El alimento no existe o no está disponible.");

        var proposal = new ProposedIntake(food.Id, food.Name, grams, mealType, date);
        return new ToolOutcome(JsonSerializer.Serialize(new
        {
            ok = true, alimento = food.Name, gramos = grams, comida = mealType.ToString(),
            fecha = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            nota = "Quedó como propuesta: el usuario la confirma o la ignora.",
        }, Json), proposal);
    }

    private static object Macros(MacroTotals t) => new { kcal = t.Calories, proteinas = t.ProteinG, carbohidratos = t.CarbsG, grasas = t.FatG };

    private static string? Text(JsonElement args, string name) =>
        args.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!.Trim()
            : null;

    // Sin "fecha" (o null) → true con null; con un texto que no es YYYY-MM-DD → false.
    private static bool TryDate(JsonElement args, out DateOnly? date)
    {
        date = null;
        if (!args.TryGetProperty("fecha", out var value) || value.ValueKind == JsonValueKind.Null) return true;
        if (value.ValueKind != JsonValueKind.String
            || !DateOnly.TryParseExact(value.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            return false;
        date = parsed;
        return true;
    }

    private static string Message(ArgumentException ex) =>
        ex.ParamName is null ? ex.Message : ex.Message.Replace($" (Parameter '{ex.ParamName}')", "");

    private static ToolOutcome Ok(object result) => new(JsonSerializer.Serialize(result, Json), null);

    private static ToolOutcome Error(string message) => new(JsonSerializer.Serialize(new { error = message }, Json), null);
}
