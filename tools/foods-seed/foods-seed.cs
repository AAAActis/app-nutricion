// Genera backend/AppNutricion.Api/SeedData/foods_seed.json desde ARGENFOODS (UNLu) y Open Food Facts Argentina.
// Uso (desde la raíz del repo): dotnet run tools/foods-seed/foods-seed.cs [--solo-argenfoods]
// Solo biblioteca estándar. Ver tools/foods-seed/README.md.
#:property PublishAot=false
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

const string UserAgent = "AppNutricion/1.0 (PLA II UNC)";
const string OutPath = "backend/AppNutricion.Api/SeedData/foods_seed.json";
const int MaxOff = 70, MaxOffBeverages = 10;

// Códigos de la tabla ARGENFOODS elegidos como básicos (https://www.argenfood.unlu.edu.ar/Tablas/Codigo/{código}.htm).
// Porciones: GAPA 2016 (Ministerio de Salud): 1 huevo = 50 g; 1 taza de leche o yogur = 200 ml (≈ 200 g, se guarda por 100 g).
var argenfoods = new (int Code, string? Portion, decimal? Grams)[]
{
    // Lácteos
    (301, "1 taza (200 ml)", 200), (303, "1 taza (200 ml)", 200), (339, "1 vaso (200 g)", 200), (341, "1 vaso (200 g)", 200),
    (308, null, null), (314, null, null), (315, null, null), (335, null, null), (338, null, null), (307, null, null),
    // Huevos
    (344, "1 huevo", 50), (345, null, null), (346, null, null), (348, null, null),
    // Carnes y pescados
    (233, null, null), (234, null, null), (229, null, null), (226, null, null), (239, null, null), (553, null, null),
    (221, null, null), (223, null, null), (216, null, null), (562, null, null), (359, null, null), (184, null, null),
    // Cereales y pastas
    (1, null, null), (2, null, null), (4, null, null), (14, null, null), (32, null, null), (47, null, null),
    // Panificados
    (36, null, null), (26, null, null), (31, null, null),
    // Legumbres y frutos secos
    (80, null, null), (77, null, null), (94, null, null), (82, null, null), (86, null, null),
    // Verduras
    (110, null, null), (87, null, null), (88, null, null), (114, null, null), (69, null, null), (430, null, null),
    (117, null, null), (75, null, null), (494, null, null), (61, null, null), (66, null, null), (104, null, null),
    // Frutas
    (122, null, null), (138, null, null), (144, null, null), (129, null, null), (132, null, null), (137, null, null),
    (146, null, null), (143, null, null), (147, null, null),
    // Aceites y grasas
    (151, null, null), (157, null, null),
    // Azúcar y dulces
    (356, null, null), (355, null, null), (351, null, null),
};

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
SelfCheck();

using var http = new HttpClient();
http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
http.Timeout = TimeSpan.FromSeconds(60);

var foods = new List<SeedFood>();
var discards = new List<string>();
var zeroed = new List<string>();   // fichas con un macro "..."/vacío tomado como 0
var nearMiss = new List<string>(); // afuera por menos del doble del margen

// ---- ARGENFOODS ----
foreach (var (code, portion, grams) in argenfoods)
{
    var bytes = await http.GetByteArrayAsync($"https://www.argenfood.unlu.edu.ar/Tablas/Codigo/{code}.htm");
    await Task.Delay(300); // sitio universitario: sin ráfagas
    var a = ParseArgenfoods(Encoding.GetEncoding(1252).GetString(bytes));
    if (a is null) { discards.Add($"Argenfoods {code}: ficha sin nombre"); continue; }
    if (a.Kcal is null) { discards.Add($"Argenfoods {code} ({a.Name}): sin energía"); continue; }
    var missing = new[] { ("proteínas", a.Protein), ("carbohidratos", a.Carbs), ("grasas", a.Fat) }
        .Where(m => m.Item2 is null).Select(m => m.Item1).ToList();
    if (missing.Count > 0)
    {
        var check = CheckZero(a.Kcal.Value, a.Protein ?? 0, a.Carbs ?? 0, a.Fat ?? 0);
        var detail = $"Argenfoods {code} ({a.Name}): {string.Join(", ", missing)} = 0 · kcal {a.Kcal:0.##} vs 4P+4C+9G {check.Atwater:0.##} (dif {check.Diff:0.##}, margen {check.Tolerance:0.##})";
        if (!check.Ok)
        {
            discards.Add($"{detail}: la kcal no confirma el 0");
            if (check.Diff <= 2 * check.Tolerance) nearMiss.Add(detail);
            continue;
        }
        zeroed.Add(detail);
    }
    foods.Add(new SeedFood(a.Name, null, null, "Argenfoods", code.ToString(CultureInfo.InvariantCulture),
        a.Kcal.Value, a.Protein ?? 0, a.Carbs ?? 0, a.Fat ?? 0, portion, grams));
}

// ---- Open Food Facts Argentina ----
// D18: 10 búsquedas/min. Dos páginas de 100 con 7 s entre llamadas.
var offFields = "code,product_name,brands,nutriments,serving_quantity,serving_size,categories_tags";
var offCount = 0; var offBeverages = 0;
var seenCodes = new HashSet<string>();
var withOff = !args.Contains("--solo-argenfoods"); // para cuando OFF está caído (503)
for (var page = 1; withOff && page <= 2 && offCount < MaxOff; page++)
{
    if (page > 1) await Task.Delay(TimeSpan.FromSeconds(7));
    // popularity_key y no unique_scans_n: con unique_scans_n los primeros son productos extranjeros con la etiqueta argentina.
    var url = $"https://world.openfoodfacts.org/api/v2/search?countries_tags=en:argentina&sort_by=popularity_key&page_size=100&page={page}&fields={offFields}";
    var root = JsonNode.Parse(await GetOff(http, url))!;
    foreach (var p in root["products"]!.AsArray())
    {
        if (offCount >= MaxOff) break;
        var code = Str(p!["code"]);
        var name = Str(p["product_name"]);
        if (code is null) { discards.Add("OpenFoodFacts (sin code): sin código"); continue; }
        if (!seenCodes.Add(code)) { discards.Add($"OpenFoodFacts {code}: código repetido"); continue; }
        if (name is null) { discards.Add($"OpenFoodFacts {code}: sin nombre"); continue; }
        var n = p["nutriments"];
        var kcal = Num(n?["energy-kcal_100g"]) ?? (Num(n?["energy-kj_100g"]) is { } kj ? kj / 4.184m : null);
        var protein = Num(n?["proteins_100g"]); var carbs = Num(n?["carbohydrates_100g"]); var fat = Num(n?["fat_100g"]);
        if (kcal is null || protein is null || carbs is null || fat is null)
        { discards.Add($"OpenFoodFacts {code} ({name}): faltan macros por 100 g"); continue; }
        var isBeverage = p["categories_tags"]?.AsArray().Any(t => Str(t) == "en:beverages") == true;
        if (isBeverage && offBeverages >= MaxOffBeverages)
        { discards.Add($"OpenFoodFacts {code} ({name}): tope de {MaxOffBeverages} bebidas"); continue; }
        var brand = Str(p["brands"])?.Split(',')[0].Trim();
        var servingQty = Num(p["serving_quantity"]);
        var servingSize = Str(p["serving_size"]);
        string? portionName = null; decimal? portionGrams = null;
        if (servingQty is >= 1 and <= 2000)
        {
            portionGrams = servingQty;
            portionName = servingSize is { Length: < 60 } ? servingSize : "1 porción";
        }
        var food = new SeedFood(name, string.IsNullOrEmpty(brand) ? null : brand, code, "OpenFoodFacts", code,
            kcal.Value, protein.Value, carbs.Value, fat.Value, portionName, portionGrams);
        if (InvalidReason(food) is { } reason) { discards.Add($"OpenFoodFacts {code} ({name}): {reason}"); continue; }
        foods.Add(food);
        offCount++;
        if (isBeverage) offBeverages++;
    }
}

// ---- Validación final (mismas reglas que FoodValidator / FoodSeedService) y escritura ----
var valid = new List<SeedFood>();
var keys = new HashSet<(string, string)>();
foreach (var f in foods.Select(Rounded))
{
    var reason = InvalidReason(f) ?? (keys.Add((f.Source, f.ExternalId)) ? null : "(source, externalId) repetido");
    if (reason is null) valid.Add(f); else discards.Add($"{f.Source} {f.ExternalId} ({f.Name}): {reason}");
}
var sorted = valid.OrderBy(f => f.Source, StringComparer.Ordinal)
    .ThenBy(f => f.Name, StringComparer.Create(new CultureInfo("es-AR"), CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace))
    .ThenBy(f => f.ExternalId, StringComparer.Ordinal).ToList();

var json = JsonSerializer.Serialize(sorted, new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    WriteIndented = true,
    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // tildes legibles; el archivo no se embebe en HTML
});
File.WriteAllText(OutPath, json + "\n", new UTF8Encoding(false));

Console.WriteLine($"Escrito {OutPath} · {DateTime.UtcNow:yyyy-MM-dd} UTC");
foreach (var g in sorted.GroupBy(f => f.Source)) Console.WriteLine($"  {g.Key}: {g.Count()}");
Console.WriteLine($"  Bebidas OFF: {offBeverages}");
Console.WriteLine($"Argenfoods con un macro tomado como 0: {zeroed.Count}");
foreach (var z in zeroed) Console.WriteLine($"  - {z}");
Console.WriteLine($"Descartados: {discards.Count}");
foreach (var d in discards) Console.WriteLine($"  - {d}");
Console.WriteLine($"Afuera por poco (dif ≤ 2 × margen, el margen no se amplía): {nearMiss.Count}");
foreach (var m in nearMiss) Console.WriteLine($"  - {m}");
if (withOff && offCount < 30) { Console.Error.WriteLine("Menos de 30 productos válidos de Open Food Facts."); return 1; }
return 0;

// "..." o vacío = 0 solo si la kcal publicada coincide con 4P + 4C + 9G (±5 % o ±5 kcal, el más amplio). Decisión de Santi, S3.
static (bool Ok, decimal Atwater, decimal Diff, decimal Tolerance) CheckZero(decimal kcal, decimal protein, decimal carbs, decimal fat)
{
    var atwater = 4 * protein + 4 * carbs + 9 * fat;
    var diff = Math.Abs(kcal - atwater);
    var tolerance = Math.Max(5m, 0.05m * kcal);
    return (diff <= tolerance, atwater, diff, tolerance);
}

// Texto de la ficha como secuencia de celdas: "Energía", "57", "kcal", "Proteínas", "3.1", "g", ...
// Un macro sin número ("..." o celda vacía) vuelve null.
static ArgenfoodsRow? ParseArgenfoods(string html)
{
    var body = Regex.Replace(html, "(?s)<head.*?</head>", "");
    var tokens = Regex.Split(body, "<[^>]*>")
        .Select(t => Regex.Replace(WebUtility.HtmlDecode(t), @"\s+", " ").Trim())
        .Where(t => t.Length > 0).ToList();
    var header = tokens.FindIndex(t => t.StartsWith("Por 100 gr", StringComparison.Ordinal));
    if (header < 1) return null;
    var name = tokens[header - 1].Split(" - ")[0].TrimEnd(' ', '-').Trim(); // saca el nombre científico ("Banana, pulpa, fresca - Musa sapientum L.")

    decimal? Value(string label, string unit)
    {
        for (var i = header; i + 2 < tokens.Count; i++)
            if (tokens[i] == label && tokens[i + 2] == unit
                && decimal.TryParse(tokens[i + 1], NumberStyles.Number, CultureInfo.InvariantCulture, out var v))
                return v;
        return null; // celda vacía: el siguiente token es la unidad, no un número
    }

    var kcal = Value("Energía", "kcal") ?? (Value("Energía", "kj") is { } kj ? kj / 4.184m : null);
    var protein = Value("Proteínas", "g");
    var fat = Value("Grasa Total", "g");
    // Disponibles si la ficha los trae; si no, totales menos fibra; si tampoco hay fibra, totales.
    var available = Value("Carbohidratos disponibles", "g");
    var total = Value("Carbohidratos totales", "g");
    var fiber = Value("Fibra dietética", "g");
    var carbs = available ?? (total is { } t && fiber is { } f ? Math.Max(0, t - f) : total);
    return new ArgenfoodsRow(name, kcal, protein, carbs, fat);
}

// Copia de FoodValidator.GetInvalidReason (BusinessLogic/Validators) para no generar ítems que la API descartaría.
static string? InvalidReason(SeedFood f)
{
    if (string.IsNullOrWhiteSpace(f.Name)) return "falta name";
    if (string.IsNullOrWhiteSpace(f.ExternalId)) return "falta externalId";
    if (f.Source is not ("Argenfoods" or "OpenFoodFacts")) return "source desconocido";
    if (f.CaloriesPer100g < 0 || f.ProteinPer100g < 0 || f.CarbsPer100g < 0 || f.FatPer100g < 0) return "macro negativo";
    if (Math.Round(f.CaloriesPer100g, 2) > 900) return "más de 900 kcal por 100 g";
    if (Math.Round(f.ProteinPer100g, 2) + Math.Round(f.CarbsPer100g, 2) + Math.Round(f.FatPer100g, 2) > 100)
        return "proteína + carbos + grasas suman más de 100 g por 100 g";
    if (f.Name.Trim().Length > 200 || f.ExternalId.Length > 64 || f.Brand?.Length > 120
        || f.Barcode?.Length > 32 || f.PortionName?.Length > 60)
        return "algún texto supera el largo de su columna";
    return null;
}

static SeedFood Rounded(SeedFood f) => f with
{
    Name = f.Name.Trim(),
    CaloriesPer100g = Math.Round(f.CaloriesPer100g, 2), ProteinPer100g = Math.Round(f.ProteinPer100g, 2),
    CarbsPer100g = Math.Round(f.CarbsPer100g, 2), FatPer100g = Math.Round(f.FatPer100g, 2),
    PortionWeightGrams = f.PortionWeightGrams is { } p ? Math.Round(p, 2) : null,
};

// OFF responde 503 cuando está cargado o se pasa el límite (D18): espera un minuto y reintenta, hasta 3 veces.
static async Task<string> GetOff(HttpClient http, string url)
{
    for (var attempt = 1; ; attempt++)
    {
        using var res = await http.GetAsync(url);
        if (res.IsSuccessStatusCode) return await res.Content.ReadAsStringAsync();
        if (res.StatusCode != HttpStatusCode.ServiceUnavailable || attempt == 3) res.EnsureSuccessStatusCode();
        Console.WriteLine($"OFF respondió 503, reintento {attempt} en 60 s");
        await Task.Delay(TimeSpan.FromSeconds(60));
    }
}

// OFF a veces manda números como string ("12.5") y textos vacíos.
static string? Str(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s) ? s.Trim() : null;
static decimal? Num(JsonNode? n)
{
    if (n is not JsonValue v) return null;
    if (v.TryGetValue<decimal>(out var d)) return d;
    if (v.TryGetValue<double>(out var dbl) && double.IsFinite(dbl)) return (decimal)dbl;
    return v.TryGetValue<string>(out var s) && decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var p) ? p : null;
}

static void SelfCheck()
{
    const string html = "<html><head><title>x</title></head><body><p class=\"T1\">Banana, pulpa, fresca - Musa sapientum L.</p>"
        + "<p>Por 100 gr de porción comestible</p><p>Energía</p><p> 414.5&nbsp;</p><p>kj&nbsp;</p><p>Energía</p><p> 99&nbsp;</p><p>kcal</p>"
        + "<p>Proteínas</p><p>1.2</p><p>g</p><p>Grasa Total</p><p>0.2</p><p>g</p><p>Carbohidratos totales</p><p>23</p><p>g</p>"
        + "<p>Carbohidratos disponibles</p><p>&nbsp;</p><p>g</p><p>Fibra dietética</p><p>&nbsp;</p><p>g</p></body></html>";
    var r = ParseArgenfoods(html) ?? throw new Exception("SelfCheck: no parseó la ficha de ejemplo");
    if (r != new ArgenfoodsRow("Banana, pulpa, fresca", 99m, 1.2m, 23m, 0.2m)) throw new Exception($"SelfCheck: ficha mal parseada {r}");
    if (ParseArgenfoods(html.Replace("<p> 99&nbsp;</p><p>kcal</p>", "")) is not { Kcal: { } k } || Math.Round(k, 2) != 99.07m)
        throw new Exception("SelfCheck: kJ → kcal");
    if (ParseArgenfoods(html.Replace("<p>0.2</p>", "<p>...</p>"))?.Fat is not null) throw new Exception("SelfCheck: '...' tiene que ser null");
    if (!CheckZero(116, 20, 0, 4).Ok) throw new Exception("SelfCheck: lomo 116 kcal = 4·20 + 9·4");
    if (!CheckZero(200, 50, 0, 1.1m).Ok || CheckZero(200, 50, 0, 1.12m).Ok) throw new Exception("SelfCheck: margen 5 %");
    if (!CheckZero(20, 1, 4, 0.55m).Ok || CheckZero(20, 1, 4, 0.57m).Ok) throw new Exception("SelfCheck: margen 5 kcal");
    var ok = new SeedFood("x", null, null, "OpenFoodFacts", "1", 100, 10, 10, 10, null, null);
    if (InvalidReason(ok) is not null) throw new Exception("SelfCheck: válido rechazado");
    if (InvalidReason(ok with { CaloriesPer100g = 900.01m }) is null) throw new Exception("SelfCheck: > 900 kcal");
    if (InvalidReason(ok with { ProteinPer100g = 50, CarbsPer100g = 50, FatPer100g = 0.01m }) is null) throw new Exception("SelfCheck: suma > 100");
    if (InvalidReason(ok with { Source = "Custom" }) is null) throw new Exception("SelfCheck: Custom");
    if (InvalidReason(ok with { Barcode = new string('1', 33) }) is null) throw new Exception("SelfCheck: largo barcode");
    if (Num(JsonValue.Create("12.5")) != 12.5m) throw new Exception("SelfCheck: número como string");
}

record ArgenfoodsRow(string Name, decimal? Kcal, decimal? Protein, decimal? Carbs, decimal? Fat);

record SeedFood(string Name, string? Brand, string? Barcode, string Source, string ExternalId,
    decimal CaloriesPer100g, decimal ProteinPer100g, decimal CarbsPer100g, decimal FatPer100g,
    string? PortionName, decimal? PortionWeightGrams);
