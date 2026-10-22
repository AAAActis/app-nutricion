using System.Net.Http.Headers;
using System.Text.Json;
using AppNutricion.DataAccess.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace AppNutricion.Tests.Infraestructura;

// Un usuario nuevo por test (ids que no chocan entre tests de la misma factory) con su cliente autenticado.
public static class UsuarioDePrueba
{
    private static int _ultimoId = 100_000;

    public static (int Id, HttpClient Client) Nuevo(ApiFactory factory)
    {
        var id = Interlocked.Increment(ref _ultimoId);
        // El token tiene que ser de un usuario que existe (la API rechaza los de cuentas borradas).
        if (factory.Services.GetService<IUserAccountStore>() is UserAccountStoreEnMemoria usuarios) usuarios.Agregar(id);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokensDePrueba.Para(id));
        return (id, client);
    }

    public static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    // Nombres de los campos con error de un ValidationProblemDetails.
    public static async Task<string[]> CamposConErrorAsync(HttpResponseMessage response) =>
        (await JsonAsync(response)).GetProperty("errors").EnumerateObject().Select(p => p.Name).ToArray();
}
