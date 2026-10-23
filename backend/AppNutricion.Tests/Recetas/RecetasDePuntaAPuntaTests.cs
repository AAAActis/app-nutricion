using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;
using AppNutricion.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using static AppNutricion.Tests.Infraestructura.UsuarioDePrueba;

namespace AppNutricion.Tests.Recetas;

// L8, L9, L11 y J5 con la API entera sobre SQLite: token real, managers y stores reales.
// La búsqueda (ILIKE) no corre en SQLite: que el Custom de otro no aparezca ahí se prueba en vivo contra Postgres.
public class RecetasDePuntaAPuntaTests(ApiFactorySqlite factory) : IClassFixture<ApiFactorySqlite>
{
    private const string Password = "una-clave-segura";
    private static readonly DateOnly Hoy = DateOnly.FromDateTime(DateTime.UtcNow);   // sin perfil, "hoy" es UTC

    private async Task<HttpClient> UsuarioAsync()
    {
        var client = factory.CreateClient();
        var email = $"u{Guid.NewGuid():N}@mail.com";
        Assert.Equal(HttpStatusCode.Created,
            (await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password, consentAccepted = true })).StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", (await JsonAsync(login)).GetProperty("accessToken").GetString());
        return client;
    }

    private async Task<int> UserIdAsync(HttpClient client) => (await JsonAsync(await client.GetAsync("/api/me"))).GetProperty("id").GetInt32();

    // Un alimento compartido (ARGENFOODS) por test: 130 kcal, 2,7 P, 28 C, 0,3 G.
    private async Task<int> ArrozAsync()
    {
        await using var context = factory.NuevoContexto();
        var arroz = new Food
        {
            Name = "Arroz", NormalizedName = "arroz", Source = FoodSource.Argenfoods, ExternalId = Guid.NewGuid().ToString("N"),
            CaloriesPer100g = 130, ProteinPer100g = 2.7m, CarbsPer100g = 28, FatPer100g = 0.3m, CreatedAt = DateTime.UtcNow,
        };
        context.Foods.Add(arroz);
        await context.SaveChangesAsync();
        return arroz.Id;
    }

    // Torta: 300 kcal, 5 P, 40 C, 12 G.
    private static async Task<int> CustomAsync(HttpClient client, string name = "Torta de la abuela")
    {
        var response = await client.PostAsJsonAsync("/api/foods/custom",
            new { name, caloriesPer100g = 300, proteinPer100g = 5, carbsPer100g = 40, fatPer100g = 12 });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await JsonAsync(response)).GetProperty("id").GetInt32();
    }

    // 200 g de arroz + 100 g de torta en 2 porciones: 300 g, 560 kcal, 10,4 P, 96 C, 12,6 G.
    private static async Task<int> RecetaAsync(HttpClient client, int arroz, int torta)
    {
        var response = await client.PostAsJsonAsync("/api/recipes", new
        {
            name = "Arroz con torta", servings = 2,
            ingredients = new[] { new { foodId = arroz, quantityGrams = 200 }, new { foodId = torta, quantityGrams = 100 } },
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await JsonAsync(response)).GetProperty("id").GetInt32();
    }

    private static Task<HttpResponseMessage> ComerRecetaAsync(HttpClient client, int recipeId, decimal servings = 1.5m, DateOnly? date = null) =>
        client.PostAsJsonAsync("/api/meals/recipe", new { recipeId, servings, mealType = "Dinner", date = (date ?? Hoy).ToString("yyyy-MM-dd") });

    [Fact]
    public async Task RecetaConUnCustomYUnCompartido_TotalesPorcionYComidaCongelada()
    {
        var yo = await UsuarioAsync();
        var receta = await RecetaAsync(yo, await ArrozAsync(), await CustomAsync(yo));

        var detalle = await JsonAsync(await yo.GetAsync($"/api/recipes/{receta}"));
        var comida = await ComerRecetaAsync(yo, receta);

        Assert.Equal(300m, detalle.GetProperty("totalGrams").GetDecimal());
        Assert.Equal(560m, detalle.GetProperty("totals").GetProperty("calories").GetDecimal());
        Assert.Equal(280m, detalle.GetProperty("perServing").GetProperty("calories").GetDecimal());
        Assert.Equal(["Arroz", "Torta de la abuela"], detalle.GetProperty("ingredients").EnumerateArray().Select(i => i.GetProperty("foodName").GetString()));
        Assert.Equal(HttpStatusCode.Created, comida.StatusCode);
        var json = await JsonAsync(comida);
        Assert.Equal((receta, "Arroz con torta", 225m, 420m, 9.45m), (json.GetProperty("recipeId").GetInt32(), json.GetProperty("itemName").GetString(),
            json.GetProperty("quantityGrams").GetDecimal(), json.GetProperty("savedCalories").GetDecimal(), json.GetProperty("savedFat").GetDecimal()));
    }

    // Resumen diario, semanal y CSV suman los Saved*: la comida de receta aparece. Copiar el día la lleva (D20).
    [Fact]
    public async Task ComidaDeReceta_ApareceEnResumenesYCsv_YSeCopia()
    {
        var yo = await UsuarioAsync();
        var receta = await RecetaAsync(yo, await ArrozAsync(), await CustomAsync(yo));
        Assert.Equal(HttpStatusCode.Created, (await ComerRecetaAsync(yo, receta)).StatusCode);

        var diario = await JsonAsync(await yo.GetAsync($"/api/summary/daily?date={Hoy:yyyy-MM-dd}"));
        var semanal = await JsonAsync(await yo.GetAsync($"/api/summary/weekly?endDate={Hoy:yyyy-MM-dd}"));
        var csv = await yo.GetStringAsync($"/api/meals/export?from={Hoy:yyyy-MM-dd}&to={Hoy:yyyy-MM-dd}");
        var copia = await yo.PostAsJsonAsync("/api/meals/copy", new { fromDate = Hoy.ToString("yyyy-MM-dd"), toDate = Hoy.AddDays(-1).ToString("yyyy-MM-dd") });

        Assert.Equal((420m, 7.8m, 72m), (diario.GetProperty("consumed").GetProperty("calories").GetDecimal(),
            diario.GetProperty("consumed").GetProperty("proteinG").GetDecimal(), diario.GetProperty("consumed").GetProperty("carbsG").GetDecimal()));
        Assert.Equal("Dinner", diario.GetProperty("byMealType")[0].GetProperty("mealType").GetString());
        Assert.Contains(semanal.GetProperty("days").EnumerateArray(), d => d.GetProperty("calories").GetDecimal() == 420m);
        // SQLite guarda decimal como texto: la escala (225 o 225.00) depende del proveedor, el valor no.
        var linea = csv.Split("\r\n")[1].Split(',');
        Assert.Equal([Hoy.ToString("yyyy-MM-dd"), "Dinner", "Arroz con torta"], linea[..3]);
        Assert.Equal([225m, 420m, 7.8m, 72m, 9.45m], linea[3..].Select(v => decimal.Parse(v, System.Globalization.CultureInfo.InvariantCulture)));
        Assert.Equal(HttpStatusCode.OK, copia.StatusCode);
        var copiada = (await JsonAsync(copia))[0];
        Assert.Equal((receta, 420m), (copiada.GetProperty("recipeId").GetInt32(), copiada.GetProperty("savedCalories").GetDecimal()));
    }

    // Editar el Custom o la receta no cambia lo ya cargado; borrar el Custom que está en una receta → 409.
    [Fact]
    public async Task EditarCustomYReceta_NoCambiaLasComidas_YBorrarElCustomDeUnaReceta409()
    {
        var yo = await UsuarioAsync();
        var arroz = await ArrozAsync();
        var torta = await CustomAsync(yo);
        var receta = await RecetaAsync(yo, arroz, torta);
        Assert.Equal(HttpStatusCode.Created, (await ComerRecetaAsync(yo, receta)).StatusCode);
        var alimento = await yo.PostAsJsonAsync("/api/meals", new { foodId = torta, quantity = 100, isUnits = false, mealType = "Snack", date = Hoy.ToString("yyyy-MM-dd") });
        Assert.Equal(HttpStatusCode.Created, alimento.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await yo.PutAsJsonAsync($"/api/foods/custom/{torta}",
            new { name = "Torta light", caloriesPer100g = 100, proteinPer100g = 5, carbsPer100g = 10, fatPer100g = 2 })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await yo.PutAsJsonAsync($"/api/recipes/{receta}",
            new { name = "Otra", servings = 1, ingredients = new[] { new { foodId = arroz, quantityGrams = 10 } } })).StatusCode);
        var borrar = await yo.DeleteAsync($"/api/foods/custom/{torta}");

        var comidas = (await JsonAsync(await yo.GetAsync($"/api/meals?date={Hoy:yyyy-MM-dd}"))).EnumerateArray()
            .Select(m => (m.GetProperty("itemName").GetString(), m.GetProperty("savedCalories").GetDecimal())).ToList();
        Assert.Equal([("Arroz con torta", 420m), ("Torta de la abuela", 300m)], comidas);
        // La receta ya no tiene la torta: ahora sí se puede borrar.
        Assert.Equal(HttpStatusCode.NoContent, borrar.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await yo.GetAsync($"/api/foods/custom/{torta}")).StatusCode);
    }

    [Fact]
    public async Task BorrarElCustomQueEstaEnUnaReceta_409ConElMensaje()
    {
        var yo = await UsuarioAsync();
        var torta = await CustomAsync(yo);
        await RecetaAsync(yo, await ArrozAsync(), torta);

        var response = await yo.DeleteAsync($"/api/foods/custom/{torta}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("El alimento está en una receta: sacalo de la receta antes de borrarlo", (await JsonAsync(response)).GetProperty("title").GetString());
    }

    // L11: nada del Custom ni de la receta de otro usuario se ve ni se usa.
    [Fact]
    public async Task ElCustomYLaRecetaDeOtro_NoSeVenNiSeUsan()
    {
        var dueño = await UsuarioAsync();
        var arroz = await ArrozAsync();
        var suTorta = await CustomAsync(dueño);
        var suReceta = await RecetaAsync(dueño, arroz, suTorta);
        Assert.Equal(HttpStatusCode.Created, (await dueño.PostAsJsonAsync("/api/meals",
            new { foodId = suTorta, quantity = 50, isUnits = false, mealType = "Snack", date = Hoy.ToString("yyyy-MM-dd") })).StatusCode);
        var yo = await UsuarioAsync();
        var anonimo = factory.CreateClient();

        var noEncontrados = new[]
        {
            await yo.GetAsync($"/api/foods/{suTorta}"),
            await anonimo.GetAsync($"/api/foods/{suTorta}"),
            await yo.GetAsync($"/api/foods/custom/{suTorta}"),
            await yo.PutAsJsonAsync($"/api/foods/custom/{suTorta}", new { name = "Mía", caloriesPer100g = 1, proteinPer100g = 0, carbsPer100g = 0, fatPer100g = 0 }),
            await yo.DeleteAsync($"/api/foods/custom/{suTorta}"),
            await yo.PostAsync($"/api/foods/{suTorta}/favorite", null),
            await yo.PostAsJsonAsync("/api/meals", new { foodId = suTorta, quantity = 100, isUnits = false, mealType = "Lunch", date = Hoy.ToString("yyyy-MM-dd") }),
            await yo.GetAsync($"/api/recipes/{suReceta}"),
            await yo.PutAsJsonAsync($"/api/recipes/{suReceta}", new { name = "Mía", servings = 1, ingredients = new[] { new { foodId = arroz, quantityGrams = 10 } } }),
            await yo.DeleteAsync($"/api/recipes/{suReceta}"),
            await ComerRecetaAsync(yo, suReceta),
        };
        var recetaConSuTorta = await yo.PostAsJsonAsync("/api/recipes",
            new { name = "Robada", servings = 1, ingredients = new[] { new { foodId = arroz, quantityGrams = 10 }, new { foodId = suTorta, quantityGrams = 10 } } });

        Assert.All(noEncontrados, r => Assert.Equal(HttpStatusCode.NotFound, r.StatusCode));
        Assert.Equal(["ingredients[1].foodId"], await CamposConErrorAsync(recetaConSuTorta));
        Assert.Equal(0, (await JsonAsync(await yo.GetAsync("/api/foods/custom"))).GetArrayLength());
        Assert.Equal(0, (await JsonAsync(await yo.GetAsync("/api/recipes"))).GetArrayLength());
        Assert.Equal(0, (await JsonAsync(await yo.GetAsync("/api/foods/favorites"))).GetArrayLength());
        Assert.Equal(0, (await JsonAsync(await yo.GetAsync("/api/foods/recent"))).GetArrayLength());
        // Y lo del dueño sigue como estaba.
        Assert.Equal("Torta de la abuela", (await JsonAsync(await dueño.GetAsync($"/api/foods/custom/{suTorta}"))).GetProperty("name").GetString());
        Assert.Equal(HttpStatusCode.OK, (await dueño.GetAsync($"/api/recipes/{suReceta}")).StatusCode);
    }

    // J5: el export trae lo nuevo y DELETE /api/me borra Custom, recetas y comidas de receta sin tocar lo del otro.
    [Fact]
    public async Task ExportYBorradoDeCuenta_ConCustomRecetasYComidasDeReceta()
    {
        var arroz = await ArrozAsync();
        var (yo, otro) = (await UsuarioAsync(), await UsuarioAsync());
        var (yoId, otroId) = (await UserIdAsync(yo), await UserIdAsync(otro));
        foreach (var client in new[] { yo, otro })
        {
            var receta = await RecetaAsync(client, arroz, await CustomAsync(client));
            await CustomAsync(client, "Suelto");
            Assert.Equal(HttpStatusCode.Created, (await ComerRecetaAsync(client, receta)).StatusCode);
        }

        var export = await JsonAsync(await yo.GetAsync("/api/me/export"));
        var borrado = await yo.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/me") { Content = JsonContent.Create(new { password = Password }) });

        Assert.Equal(["Torta de la abuela", "Suelto"], export.GetProperty("customFoods").EnumerateArray().Select(f => f.GetProperty("name").GetString()));
        var receta0 = export.GetProperty("recipes")[0];
        Assert.Equal(("Arroz con torta", 2), (receta0.GetProperty("name").GetString(), receta0.GetProperty("servings").GetInt32()));
        Assert.Equal(["Arroz", "Torta de la abuela"], receta0.GetProperty("ingredients").EnumerateArray().Select(i => i.GetProperty("foodName").GetString()));
        Assert.Equal(receta0.GetProperty("id").GetInt32(), export.GetProperty("mealEntries")[0].GetProperty("recipeId").GetInt32());
        Assert.Equal(HttpStatusCode.NoContent, borrado.StatusCode);

        await using var context = factory.NuevoContexto();
        Assert.False(await context.Users.AnyAsync(u => u.Id == yoId));
        Assert.False(await context.Foods.AnyAsync(f => f.CreatedByUserId == yoId));
        Assert.False(await context.Recipes.AnyAsync(r => r.UserId == yoId));
        Assert.False(await context.MealEntries.AnyAsync(m => m.UserId == yoId));
        Assert.True(await context.Foods.AnyAsync(f => f.Id == arroz));
        Assert.Equal(2, await context.Foods.CountAsync(f => f.CreatedByUserId == otroId));
        var suReceta = await context.Recipes.Include(r => r.Ingredients).SingleAsync(r => r.UserId == otroId);
        Assert.Equal(2, suReceta.Ingredients.Count);
        Assert.Equal(suReceta.Id, (await context.MealEntries.SingleAsync(m => m.UserId == otroId)).RecipeId);
    }
}
