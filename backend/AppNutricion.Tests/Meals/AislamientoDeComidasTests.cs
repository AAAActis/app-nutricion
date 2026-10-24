using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;
using AppNutricion.Tests.Infraestructura;
using static AppNutricion.Tests.Infraestructura.UsuarioDePrueba;

namespace AppNutricion.Tests.Meals;

// L11 (caracterización): los Stores ya filtran por UserId (MealEntryStoreTests, ResumenStoreTests) y los controllers
// pasan el del token (con mocks). Esto prueba lo mismo punta a punta: token real, managers y stores reales sobre SQLite.
public class AislamientoDeComidasTests(ApiFactorySqlite factory) : IClassFixture<ApiFactorySqlite>
{
    private const string Password = "una-clave-segura";
    private static readonly DateOnly Hoy = DateOnly.FromDateTime(DateTime.UtcNow);   // sin perfil, "hoy" es UTC
    private static readonly string HoyTexto = Hoy.ToString("yyyy-MM-dd");

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

    [Fact]
    public async Task LaComidaDeOtro_NoApareceEnListaResumenesNiCsv_NiSeEditaBorraOCopia()
    {
        var dueño = await UsuarioAsync();
        var creada = await dueño.PostAsJsonAsync("/api/meals",
            new { foodId = await ArrozAsync(), quantity = 200, isUnits = false, mealType = "Lunch", date = HoyTexto });
        Assert.Equal(HttpStatusCode.Created, creada.StatusCode);
        var suComida = (await JsonAsync(creada)).GetProperty("id").GetInt32();
        var yo = await UsuarioAsync();

        var lista = await JsonAsync(await yo.GetAsync($"/api/meals?date={HoyTexto}"));
        var diario = await JsonAsync(await yo.GetAsync($"/api/summary/daily?date={HoyTexto}"));
        var semanal = await JsonAsync(await yo.GetAsync($"/api/summary/weekly?endDate={HoyTexto}"));
        var csv = await yo.GetStringAsync($"/api/meals/export?from={HoyTexto}&to={HoyTexto}");
        var editar = await yo.PutAsJsonAsync($"/api/meals/{suComida}", new { quantity = 1, isUnits = false });
        var borrar = await yo.DeleteAsync($"/api/meals/{suComida}");
        await yo.PostAsJsonAsync("/api/meals/copy", new { fromDate = HoyTexto, toDate = Hoy.AddDays(-1).ToString("yyyy-MM-dd") });

        Assert.Equal(0, lista.GetArrayLength());
        Assert.Equal(0m, diario.GetProperty("consumed").GetProperty("calories").GetDecimal());
        Assert.Equal(0, semanal.GetProperty("daysLogged").GetInt32());
        Assert.Single(csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries));   // solo la cabecera
        Assert.Equal(HttpStatusCode.NotFound, editar.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, borrar.StatusCode);
        Assert.Equal(0, (await JsonAsync(await yo.GetAsync($"/api/meals?date={Hoy.AddDays(-1):yyyy-MM-dd}"))).GetArrayLength());
        // Y la del dueño sigue como estaba.
        var suya = Assert.Single((await JsonAsync(await dueño.GetAsync($"/api/meals?date={HoyTexto}"))).EnumerateArray());
        Assert.Equal((suComida, 200m), (suya.GetProperty("id").GetInt32(), suya.GetProperty("quantityGrams").GetDecimal()));
    }
}
