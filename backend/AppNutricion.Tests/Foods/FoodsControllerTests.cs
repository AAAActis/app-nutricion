using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AppNutricion.Entities.Entities;
using AppNutricion.Tests.Infraestructura;
using Moq;

namespace AppNutricion.Tests.Foods;

public class FoodsControllerTests : IClassFixture<ApiFactoryAutenticada>
{
    private readonly ApiFactoryAutenticada _factory;

    public FoodsControllerTests(ApiFactoryAutenticada factory)
    {
        _factory = factory;
        _factory.FoodManager.Reset();
    }

    [Fact]
    public async Task GetByBarcode_CodigoInvalido_Devuelve400ValidationProblemConElCampo()
    {
        _factory.FoodManager.Setup(m => m.GetByBarcodeAsync("abc", null))
            .ThrowsAsync(new ArgumentException("El código de barras debe tener entre 8 y 14 dígitos.", "code"));

        var response = await _factory.CrearCliente(null).GetAsync("/api/foods/barcode/abc");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(["code"], await UsuarioDePrueba.CamposConErrorAsync(response));
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("entre 8 y 14", body);
        Assert.DoesNotContain("(Parameter", body);
    }

    private const int UserId = 7;

    public static TheoryData<string, string> EndpointsConAuth => new()
    {
        { "POST", "/api/foods/3/favorite" },
        { "DELETE", "/api/foods/3/favorite" },
        { "GET", "/api/foods/favorites" },
        { "GET", "/api/foods/recent" },
    };

    [Theory]
    [MemberData(nameof(EndpointsConAuth))]
    public async Task EndpointsNuevos_SinHeader_Devuelven401(string metodo, string ruta)
    {
        var response = await _factory.CrearCliente(null).SendAsync(new HttpRequestMessage(new HttpMethod(metodo), ruta));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(TestAuthHandler.Esquema, response.Headers.WwwAuthenticate.ToString());   // lo pidió [Authorize]
        Assert.Empty(_factory.FoodManager.Invocations);
    }

    // Autenticado pero sin un "sub" entero: es un bug de configuración (500), no un 401.
    [Theory]
    [MemberData(nameof(EndpointsConAuth))]
    public async Task EndpointsNuevos_UserIdQueNoEsEntero_Devuelven500SinLlegarAlManager(string metodo, string ruta)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.Header, "abc");

        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(metodo), ruta));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Empty(_factory.FoodManager.Invocations);
    }

    [Theory]
    [MemberData(nameof(EndpointsConAuth))]
    public async Task EndpointsNuevos_ConNameIdentifierYSinSub_NoLleganAlManager(string metodo, string ruta)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderNameIdentifier, UserId.ToString());

        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(metodo), ruta));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Empty(_factory.FoodManager.Invocations);
    }

    // Públicos: sin token el userId es null; con token, el del token (para incluir sus alimentos propios).
    [Fact]
    public async Task Publicos_ConToken_PasanElUserIdDelToken()
    {
        _factory.FoodManager.Setup(m => m.SearchAsync("pan", UserId, null)).ReturnsAsync([]);
        var client = _factory.CrearCliente(UserId);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/foods/search?q=pan")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/foods/3")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/foods/barcode/7790001000001")).StatusCode);
        _factory.FoodManager.Verify(m => m.SearchAsync("pan", UserId, null), Times.Once);
        _factory.FoodManager.Verify(m => m.GetByIdAsync(3, UserId), Times.Once);
        _factory.FoodManager.Verify(m => m.GetByBarcodeAsync("7790001000001", UserId), Times.Once);
    }

    [Fact]
    public async Task BusquedaYDetalle_SiguenPublicos()
    {
        _factory.FoodManager.Setup(m => m.SearchAsync("pan", null, null)).ReturnsAsync([]);
        var client = _factory.CrearCliente(null);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/foods/search?q=pan")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/foods/3")).StatusCode);
        _factory.FoodManager.Verify(m => m.GetByIdAsync(3, null), Times.Once);
    }

    [Fact]
    public async Task PostFavorite_Visible_Devuelve204ConElUserIdDelClaim()
    {
        _factory.FoodManager.Setup(m => m.AddFavoriteAsync(3, UserId)).ReturnsAsync(true);

        var response = await _factory.CrearCliente(UserId).PostAsync("/api/foods/3/favorite", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task PostFavorite_NoVisible_Devuelve404ProblemDetails()
    {
        var response = await _factory.CrearCliente(UserId).PostAsync("/api/foods/3/favorite", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        _factory.FoodManager.Verify(m => m.AddFavoriteAsync(3, UserId), Times.Once);
    }

    [Fact]
    public async Task DeleteFavorite_Visible_Devuelve204ConElUserIdDelClaim()
    {
        _factory.FoodManager.Setup(m => m.RemoveFavoriteAsync(3, UserId)).ReturnsAsync(true);

        var response = await _factory.CrearCliente(UserId).DeleteAsync("/api/foods/3/favorite");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteFavorite_NoVisible_Devuelve404ProblemDetails()
    {
        var response = await _factory.CrearCliente(UserId).DeleteAsync("/api/foods/3/favorite");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        _factory.FoodManager.Verify(m => m.RemoveFavoriteAsync(3, UserId), Times.Once);
    }

    [Fact]
    public async Task GetFavorites_DevuelveLosDelUsuarioDelClaim()
    {
        _factory.FoodManager.Setup(m => m.GetFavoritesAsync(UserId)).ReturnsAsync([new Food { Id = 3, Name = "Pan" }]);

        var response = await _factory.CrearCliente(UserId).GetAsync("/api/foods/favorites");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Pan", Assert.Single(json.EnumerateArray()).GetProperty("name").GetString());
    }

    [Fact]
    public async Task GetRecent_PasaElLimiteYElUserIdDelClaim()
    {
        _factory.FoodManager.Setup(m => m.GetRecentAsync(UserId, 5)).ReturnsAsync([new Food { Id = 3, Name = "Pan" }]);

        var response = await _factory.CrearCliente(UserId).GetAsync("/api/foods/recent?limit=5");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, Assert.Single(json.EnumerateArray()).GetProperty("id").GetInt32());
    }

    [Theory]
    [InlineData(7)]
    [InlineData(42)]
    public async Task GetFavorites_ElUserIdQueLlegaAlManagerEsElDelClaim(int userId)
    {
        _factory.FoodManager.Setup(m => m.GetFavoritesAsync(userId)).ReturnsAsync([]);

        var response = await _factory.CrearCliente(userId).GetAsync("/api/foods/favorites");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _factory.FoodManager.Verify(m => m.GetFavoritesAsync(userId), Times.Once);
    }
}
