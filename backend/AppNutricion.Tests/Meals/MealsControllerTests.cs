using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AppNutricion.BusinessLogic.Exceptions;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;
using AppNutricion.Tests.Infraestructura;
using Moq;

namespace AppNutricion.Tests.Meals;

public class MealsControllerTests : IClassFixture<ApiFactoryAutenticada>
{
    private const int UserId = 7;
    private static readonly DateOnly Dia = new(2026, 10, 20);

    private readonly ApiFactoryAutenticada _factory;
    private readonly MealEntry _entrada = new()
    {
        Id = 5, UserId = UserId, FoodId = 3, ItemName = "Pan (Bimbo)", Date = Dia, MealType = MealType.Lunch,
        QuantityGrams = 150, SavedCalories = 375, SavedProtein = 15, SavedCarbs = 45, SavedFat = 7.5m,
        CreatedAt = new DateTime(2026, 10, 20, 15, 0, 0, DateTimeKind.Utc),
    };

    public MealsControllerTests(ApiFactoryAutenticada factory)
    {
        _factory = factory;
        _factory.MealManager.Reset();
    }

    private static object Alta(object? userId = null) => new
    {
        foodId = 3, quantity = 150, isUnits = false, mealType = "Lunch", date = "2026-10-20", userId,
    };

    public static TheoryData<string, string> EndpointsDeComidas => new()
    {
        { "POST", "/api/meals" },
        { "PUT", "/api/meals/5" },
        { "DELETE", "/api/meals/5" },
        { "GET", "/api/meals?date=2026-10-20" },
        { "POST", "/api/meals/copy" },
        { "GET", "/api/meals/export?from=2026-10-19&to=2026-10-20" },
    };

    private static HttpRequestMessage Pedido(string metodo, string ruta) => new(new HttpMethod(metodo), ruta)
    {
        Content = ruta.EndsWith("/copy") ? JsonContent.Create(new { fromDate = "2026-10-19", toDate = "2026-10-20" })
            : metodo is "POST" or "PUT" ? JsonContent.Create(Alta()) : null,
    };

    [Theory]
    [MemberData(nameof(EndpointsDeComidas))]
    public async Task SinHeader_Devuelve401(string metodo, string ruta)
    {
        var response = await _factory.CrearCliente(null).SendAsync(Pedido(metodo, ruta));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(TestAuthHandler.Esquema, response.Headers.WwwAuthenticate.ToString());   // lo pidió [Authorize]
        Assert.Empty(_factory.MealManager.Invocations);
    }

    // Autenticado pero sin un "sub" entero: es un bug de configuración (500), no un 401.
    [Theory]
    [MemberData(nameof(EndpointsDeComidas))]
    public async Task UserIdQueNoEsEntero_Devuelve500SinLlegarAlManager(string metodo, string ruta)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.Header, "abc");

        var response = await client.SendAsync(Pedido(metodo, ruta));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Empty(_factory.MealManager.Invocations);
    }

    // El userId sale solo de "sub" (MapInboundClaims = false): si un controller volviera a leer NameIdentifier,
    // este test llegaría al manager. Y si TestAuthHandler volviera a emitir NameIdentifier, fallaría todo lo demás.
    [Theory]
    [MemberData(nameof(EndpointsDeComidas))]
    public async Task ConNameIdentifierYSinSub_NoLlegaAlManager(string metodo, string ruta)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderNameIdentifier, UserId.ToString());

        var response = await client.SendAsync(Pedido(metodo, ruta));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Empty(_factory.MealManager.Invocations);
    }

    [Fact]
    public async Task Post_UsaElUserIdDelClaimAunqueElBodyTraigaOtro_Y_Devuelve201()
    {
        _factory.MealManager
            .Setup(m => m.CreateAsync(UserId, 3, 150, false, MealType.Lunch, Dia, null))
            .ReturnsAsync(_entrada);

        var response = await _factory.CrearCliente(UserId).PostAsJsonAsync("/api/meals", Alta(userId: 99));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(5, json.RootElement.GetProperty("id").GetInt32());
        Assert.Equal("Lunch", json.RootElement.GetProperty("mealType").GetString());
        Assert.Equal("2026-10-20", json.RootElement.GetProperty("date").GetString());
        Assert.Equal(375m, json.RootElement.GetProperty("savedCalories").GetDecimal());
        _factory.MealManager.Verify(m => m.CreateAsync(99, It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<bool>(),
            It.IsAny<MealType>(), It.IsAny<DateOnly>(), It.IsAny<int?>()), Times.Never);
    }

    [Fact]
    public async Task Post_ManagerDevuelveNull_Devuelve404ProblemDetails()
    {
        var response = await _factory.CrearCliente(UserId).PostAsJsonAsync("/api/meals", Alta());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        _factory.MealManager.Verify(m => m.CreateAsync(UserId, 3, 150, false, MealType.Lunch, Dia, null), Times.Once);
    }

    [Fact]
    public async Task Post_ManagerLanzaArgumentException_Devuelve400ValidationProblemConElCampo()
    {
        _factory.MealManager
            .Setup(m => m.CreateAsync(UserId, It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<bool>(),
                It.IsAny<MealType>(), It.IsAny<DateOnly>(), It.IsAny<int?>()))
            .ThrowsAsync(new ArgumentException("cantidad fuera de rango", "quantity"));

        var response = await _factory.CrearCliente(UserId).PostAsJsonAsync("/api/meals", Alta());

        await AssertValidacionAsync(response, "quantity", "cantidad fuera de rango");
    }

    // 400 en el formato de J2: ValidationProblemDetails con el campo y el mensaje sin " (Parameter 'x')".
    private static async Task AssertValidacionAsync(HttpResponseMessage response, string campo, string mensaje)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal([campo], await UsuarioDePrueba.CamposConErrorAsync(response));
        var errores = (await UsuarioDePrueba.JsonAsync(response)).GetProperty("errors").GetProperty(campo);
        Assert.Equal(mensaje, Assert.Single(errores.EnumerateArray()).GetString());
        Assert.DoesNotContain("(Parameter", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Post_ConRecipeId_LoPasaAlManager()
    {
        var response = await _factory.CrearCliente(UserId).PostAsJsonAsync("/api/meals",
            new { foodId = 3, quantity = 150, isUnits = false, mealType = "Lunch", date = "2026-10-20", recipeId = 4 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        _factory.MealManager.Verify(m => m.CreateAsync(UserId, 3, 150, false, MealType.Lunch, Dia, 4), Times.Once);
    }

    [Fact]
    public async Task Put_PasaLosCamposOpcionalesYElUserIdDelClaim()
    {
        _factory.MealManager.Setup(m => m.UpdateAsync(5, UserId, 300, null, MealType.Dinner, null)).ReturnsAsync(_entrada);

        var response = await _factory.CrearCliente(UserId).PutAsJsonAsync("/api/meals/5",
            new { quantity = 300, mealType = "Dinner", userId = 99 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(5, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task Put_DeOtroUsuarioOInexistente_Devuelve404ProblemDetails()
    {
        var response = await _factory.CrearCliente(UserId).PutAsJsonAsync("/api/meals/5", new { quantity = 300 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        _factory.MealManager.Verify(m => m.UpdateAsync(5, UserId, 300, null, null, null), Times.Once);
    }

    [Fact]
    public async Task Put_ManagerLanzaArgumentException_Devuelve400ValidationProblemConElCampo()
    {
        _factory.MealManager.Setup(m => m.UpdateAsync(5, UserId, It.IsAny<decimal?>(), It.IsAny<bool?>(),
                It.IsAny<MealType?>(), It.IsAny<DateOnly?>()))
            .ThrowsAsync(new ArgumentException("sin porción", "isUnits"));

        var response = await _factory.CrearCliente(UserId).PutAsJsonAsync("/api/meals/5", new { quantity = 2, isUnits = true });

        await AssertValidacionAsync(response, "isUnits", "sin porción");
    }

    [Fact]
    public async Task Delete_Existente_Devuelve204()
    {
        _factory.MealManager.Setup(m => m.DeleteAsync(5, UserId)).ReturnsAsync(true);

        var response = await _factory.CrearCliente(UserId).DeleteAsync("/api/meals/5");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Delete_DeOtroUsuarioOInexistente_Devuelve404ProblemDetails()
    {
        var response = await _factory.CrearCliente(UserId).DeleteAsync("/api/meals/5");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        _factory.MealManager.Verify(m => m.DeleteAsync(5, UserId), Times.Once);
    }

    [Fact]
    public async Task Get_DevuelveLasEntradasDelDiaDelUsuario()
    {
        _factory.MealManager.Setup(m => m.ListAsync(UserId, Dia)).ReturnsAsync([_entrada]);

        var response = await _factory.CrearCliente(UserId).GetAsync("/api/meals?date=2026-10-20");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Pan (Bimbo)", Assert.Single(json.EnumerateArray()).GetProperty("itemName").GetString());
    }

    [Fact]
    public async Task Get_SinFecha_Devuelve400ValidationProblemConElCampo()
    {
        _factory.MealManager.Setup(m => m.ListAsync(UserId, null)).ThrowsAsync(new ArgumentException("Falta la fecha", "date"));

        var response = await _factory.CrearCliente(UserId).GetAsync("/api/meals");

        await AssertValidacionAsync(response, "date", "Falta la fecha");
    }

    [Fact]
    public async Task Copy_PasaFechasTipoYElUserIdDelClaim_Devuelve200ConLasCopias()
    {
        _factory.MealManager.Setup(m => m.CopyAsync(UserId, Dia.AddDays(-1), Dia, MealType.Lunch)).ReturnsAsync([_entrada]);

        var response = await _factory.CrearCliente(UserId).PostAsJsonAsync("/api/meals/copy",
            new { fromDate = "2026-10-19", toDate = "2026-10-20", mealType = "Lunch", userId = 99 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(5, Assert.Single(json.EnumerateArray()).GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task Copy_SinMealType_PasaNull()
    {
        _factory.MealManager.Setup(m => m.CopyAsync(UserId, Dia.AddDays(-1), Dia, null)).ReturnsAsync([]);

        var response = await _factory.CrearCliente(UserId).PostAsJsonAsync("/api/meals/copy",
            new { fromDate = "2026-10-19", toDate = "2026-10-20" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());
        _factory.MealManager.Verify(m => m.CopyAsync(UserId, Dia.AddDays(-1), Dia, null), Times.Once);
    }

    [Fact]
    public async Task Copy_DiaDestinoConComidas_Devuelve409ProblemDetailsConElMensaje()
    {
        _factory.MealManager.Setup(m => m.CopyAsync(UserId, Dia.AddDays(-1), Dia, null))
            .ThrowsAsync(new ConflictException("El día destino ya tiene comidas cargadas"));

        var response = await _factory.CrearCliente(UserId).PostAsJsonAsync("/api/meals/copy",
            new { fromDate = "2026-10-19", toDate = "2026-10-20" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("El día destino ya tiene comidas cargadas", (await UsuarioDePrueba.JsonAsync(response)).GetProperty("title").GetString());
    }

    [Fact]
    public async Task Copy_ManagerLanzaArgumentException_Devuelve400ValidationProblemConElCampo()
    {
        _factory.MealManager.Setup(m => m.CopyAsync(UserId, Dia, Dia, null)).ThrowsAsync(new ArgumentException("mismo día", "toDate"));

        var response = await _factory.CrearCliente(UserId).PostAsJsonAsync("/api/meals/copy",
            new { fromDate = "2026-10-20", toDate = "2026-10-20" });

        await AssertValidacionAsync(response, "toDate", "mismo día");
    }

    [Theory]
    [InlineData(7)]
    [InlineData(42)]
    public async Task Get_ElUserIdQueLlegaAlManagerEsElDelClaim(int userId)
    {
        _factory.MealManager.Setup(m => m.ListAsync(userId, Dia)).ReturnsAsync([_entrada]);

        var response = await _factory.CrearCliente(userId).GetAsync("/api/meals?date=2026-10-20");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _factory.MealManager.Verify(m => m.ListAsync(userId, Dia), Times.Once);
    }

    [Fact]
    public async Task Export_CsvConBomComoAdjuntoConElUserIdDelToken()
    {
        const string csv = "fecha,alimento\r\n2026-10-20,Ñoquis\r\n";
        _factory.MealManager.Setup(m => m.ExportCsvAsync(UserId, new DateOnly(2026, 10, 19), Dia)).ReturnsAsync(csv);

        var response = await _factory.CrearCliente(UserId).GetAsync("/api/meals/export?from=2026-10-19&to=2026-10-20");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("utf-8", response.Content.Headers.ContentType.CharSet);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal("comidas_2026-10-19_2026-10-20.csv", response.Content.Headers.ContentDisposition.FileName!.Trim('"'));
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]);   // BOM: Excel lee bien las tildes
        Assert.Equal(csv, System.Text.Encoding.UTF8.GetString(bytes[3..]));
    }

    [Fact]
    public async Task Export_RangoInvalido_Devuelve400ConElCampo()
    {
        _factory.MealManager.Setup(m => m.ExportCsvAsync(UserId, It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>()))
            .ThrowsAsync(new ArgumentException("El rango es de hasta 366 días.", "to"));

        var response = await _factory.CrearCliente(UserId).GetAsync("/api/meals/export?from=2020-01-01&to=2026-10-20");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(["to"], await UsuarioDePrueba.CamposConErrorAsync(response));
    }
}
