using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AppNutricion.BusinessLogic.Exceptions;
using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;
using AppNutricion.Tests.Infraestructura;
using Moq;

namespace AppNutricion.Tests.Asistente;

public class AssistantControllerTests : IClassFixture<ApiFactoryAsistente>
{
    private const int UserId = 7;
    private static readonly DateTime Base = new(2026, 10, 22, 15, 0, 0, DateTimeKind.Utc);

    private readonly ApiFactoryAsistente _factory;
    private static readonly Food Arroz = new() { Id = 2, Name = "Arroz hervido" };

    public AssistantControllerTests(ApiFactoryAsistente factory)
    {
        _factory = factory;
        _factory.AssistantManager.Reset();
    }

    public static TheoryData<string, string> Endpoints => new()
    {
        { "POST", "/api/assistant/conversations" },
        { "GET", "/api/assistant/conversations" },
        { "GET", "/api/assistant/conversations/3/messages" },
        { "POST", "/api/assistant/conversations/3/messages" },
        { "POST", "/api/assistant/suggestions/5/accept" },
        { "POST", "/api/assistant/suggestions/5/ignore" },
    };

    private static HttpRequestMessage Pedido(string metodo, string ruta) => new(new HttpMethod(metodo), ruta)
    {
        Content = ruta.EndsWith("/messages") && metodo == "POST" ? JsonContent.Create(new { content = "hola" }) : null,
    };

    internal static AssistantSuggestion Sugerencia(SuggestionStatus status = SuggestionStatus.Pending, int? mealEntryId = null) => new()
    {
        Id = 5, UserId = UserId, ChatMessageId = 9, FoodId = Arroz.Id, Food = Arroz, QuantityGrams = 150, MealType = MealType.Lunch,
        Date = new DateOnly(2026, 10, 22), Status = status, CreatedAt = Base, MealEntryId = mealEntryId,
        ResolvedAt = status == SuggestionStatus.Pending ? null : Base.AddMinutes(1),
    };

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    private static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal((int)status, (await Json(response)).GetProperty("status").GetInt32());
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task SinHeader_Devuelve401(string metodo, string ruta)
    {
        var response = await _factory.CrearCliente(null).SendAsync(Pedido(metodo, ruta));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(TestAuthHandler.Esquema, response.Headers.WwwAuthenticate.ToString());
        Assert.Empty(_factory.AssistantManager.Invocations);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task ConNameIdentifierYSinSub_NoLlegaAlManager(string metodo, string ruta)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderNameIdentifier, UserId.ToString());

        var response = await client.SendAsync(Pedido(metodo, ruta));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Empty(_factory.AssistantManager.Invocations);
    }

    // ---- Conversaciones ----

    [Fact]
    public async Task CrearConversacion_201()
    {
        _factory.AssistantManager.Setup(m => m.CreateConversationAsync(UserId))
            .ReturnsAsync(new Conversation { Id = 3, UserId = UserId, CreatedAt = Base, UpdatedAt = Base });

        var response = await _factory.CrearCliente(UserId).PostAsync("/api/assistant/conversations", null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await Json(response);
        Assert.Equal(3, json.GetProperty("id").GetInt32());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("title").ValueKind);
    }

    [Fact]
    public async Task ListarConversaciones_DelUsuario()
    {
        _factory.AssistantManager.Setup(m => m.ListConversationsAsync(UserId))
            .ReturnsAsync([new Conversation { Id = 4, Title = "comí arroz", CreatedAt = Base, UpdatedAt = Base.AddMinutes(5) }]);

        var response = await _factory.CrearCliente(UserId).GetAsync("/api/assistant/conversations");

        var item = Assert.Single((await Json(response)).EnumerateArray());
        Assert.Equal((4, "comí arroz"), (item.GetProperty("id").GetInt32(), item.GetProperty("title").GetString()));
        Assert.Equal(Base.AddMinutes(5), item.GetProperty("updatedAt").GetDateTime());
    }

    [Fact]
    public async Task Mensajes_ConSusSugerencias()
    {
        _factory.AssistantManager.Setup(m => m.GetHistoryAsync(3, UserId)).ReturnsAsync(new ConversationHistory(
            [
                new ChatMessage { Id = 8, Role = ChatRole.User, Content = "comí 150 g de arroz", CreatedAt = Base },
                new ChatMessage { Id = 9, Role = ChatRole.Assistant, Content = "Te propuse…", CreatedAt = Base },
            ],
            [Sugerencia(SuggestionStatus.Accepted, 77)]));

        var response = await _factory.CrearCliente(UserId).GetAsync("/api/assistant/conversations/3/messages");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var mensajes = (await Json(response)).EnumerateArray().ToList();
        Assert.Equal(["User", "Assistant"], mensajes.Select(m => m.GetProperty("role").GetString()));
        Assert.Empty(mensajes[0].GetProperty("suggestions").EnumerateArray());
        var s = Assert.Single(mensajes[1].GetProperty("suggestions").EnumerateArray());
        Assert.Equal((5, "Arroz hervido", 150m, "Lunch", "Accepted", 77),
            (s.GetProperty("id").GetInt32(), s.GetProperty("foodName").GetString(), s.GetProperty("quantityGrams").GetDecimal(),
             s.GetProperty("mealType").GetString(), s.GetProperty("status").GetString(), s.GetProperty("mealEntryId").GetInt32()));
    }

    [Fact]
    public async Task MensajesDeOtraConversacion_404Problem()
    {
        await AssertProblem(await _factory.CrearCliente(UserId).GetAsync("/api/assistant/conversations/3/messages"), HttpStatusCode.NotFound);
        _factory.AssistantManager.Verify(m => m.GetHistoryAsync(3, UserId), Times.Once);
    }

    // ---- Enviar ----

    [Fact]
    public async Task Enviar_RespuestaConSugerenciasYDegraded()
    {
        _factory.AssistantManager.Setup(m => m.SendAsync(UserId, 3, "comí 150 g de arroz", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssistantReply(new ChatMessage { Id = 9, Role = ChatRole.Assistant, Content = "Te propuse registrar 150 g", CreatedAt = Base },
                [Sugerencia()], false));

        var response = await _factory.CrearCliente(UserId).PostAsJsonAsync("/api/assistant/conversations/3/messages", new { content = "comí 150 g de arroz" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await Json(response);
        Assert.Equal("Te propuse registrar 150 g", json.GetProperty("message").GetProperty("content").GetString());
        Assert.Equal(9, json.GetProperty("message").GetProperty("id").GetInt32());
        Assert.False(json.GetProperty("degraded").GetBoolean());
        Assert.Equal("Pending", Assert.Single(json.GetProperty("suggestions").EnumerateArray()).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Enviar_TextoInvalido_400Content()
    {
        _factory.AssistantManager.Setup(m => m.SendAsync(UserId, 3, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("El mensaje es obligatorio y de hasta 1000 caracteres.", "content"));

        var response = await _factory.CrearCliente(UserId).PostAsJsonAsync("/api/assistant/conversations/3/messages", new { content = "" });

        await AssertProblem(response, HttpStatusCode.BadRequest);
        Assert.True((await Json(response)).GetProperty("errors").TryGetProperty("content", out _));
    }

    [Fact]
    public async Task Enviar_ConversacionDeOtro_404Problem()
    {
        await AssertProblem(await _factory.CrearCliente(UserId).PostAsJsonAsync("/api/assistant/conversations/3/messages", new { content = "hola" }),
            HttpStatusCode.NotFound);
    }

    // ---- Sugerencias ----

    [Fact]
    public async Task Aceptar_200ConLaComida_404Y409()
    {
        _factory.AssistantManager.Setup(m => m.AcceptSuggestionAsync(5, UserId)).ReturnsAsync(Sugerencia(SuggestionStatus.Accepted, 77));
        _factory.AssistantManager.Setup(m => m.AcceptSuggestionAsync(6, UserId)).ThrowsAsync(new ConflictException("La sugerencia ya fue resuelta."));
        _factory.AssistantManager.Setup(m => m.AcceptSuggestionAsync(8, UserId)).ThrowsAsync(new ConflictException("El alimento ya no está disponible"));
        var client = _factory.CrearCliente(UserId);

        var ok = await client.PostAsync("/api/assistant/suggestions/5/accept", null);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(("Accepted", 77), ((await Json(ok)).GetProperty("status").GetString(), (await Json(ok)).GetProperty("mealEntryId").GetInt32()));
        await AssertProblem(await client.PostAsync("/api/assistant/suggestions/7/accept", null), HttpStatusCode.NotFound);
        var resuelta = await client.PostAsync("/api/assistant/suggestions/6/accept", null);
        await AssertProblem(resuelta, HttpStatusCode.Conflict);
        Assert.Equal("La sugerencia ya fue resuelta.", (await Json(resuelta)).GetProperty("title").GetString());
        var sinAlimento = await client.PostAsync("/api/assistant/suggestions/8/accept", null);
        Assert.Equal("El alimento ya no está disponible", (await Json(sinAlimento)).GetProperty("title").GetString());
    }

    [Fact]
    public async Task Aceptar_FechaYaFueraDelDiario_400ConElCampo()
    {
        _factory.AssistantManager.Setup(m => m.AcceptSuggestionAsync(5, UserId))
            .ThrowsAsync(new ArgumentException("La fecha no puede ser futura ni de hace más de 2 años.", "date"));

        var response = await _factory.CrearCliente(UserId).PostAsync("/api/assistant/suggestions/5/accept", null);

        await AssertProblem(response, HttpStatusCode.BadRequest);
        Assert.True((await Json(response)).GetProperty("errors").TryGetProperty("date", out _));
    }

    [Fact]
    public async Task Ignorar_200_404Y409()
    {
        _factory.AssistantManager.Setup(m => m.IgnoreSuggestionAsync(5, UserId)).ReturnsAsync(Sugerencia(SuggestionStatus.Ignored));
        _factory.AssistantManager.Setup(m => m.IgnoreSuggestionAsync(6, UserId)).ThrowsAsync(new ConflictException("La sugerencia ya fue resuelta."));
        var client = _factory.CrearCliente(UserId);

        var ok = await client.PostAsync("/api/assistant/suggestions/5/ignore", null);
        Assert.Equal("Ignored", (await Json(ok)).GetProperty("status").GetString());
        await AssertProblem(await client.PostAsync("/api/assistant/suggestions/7/ignore", null), HttpStatusCode.NotFound);
        await AssertProblem(await client.PostAsync("/api/assistant/suggestions/6/ignore", null), HttpStatusCode.Conflict);
    }
}

public class AssistantRateLimitTests(ApiFactoryAsistenteRateLimit factory) : IClassFixture<ApiFactoryAsistenteRateLimit>
{
    [Fact]
    public async Task LaVuelta21DelMinuto_429_PorUsuario()
    {
        factory.AssistantManager.Setup(m => m.SendAsync(It.IsAny<int>(), 3, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssistantReply(new ChatMessage { Id = 1, Role = ChatRole.Assistant, Content = "ok" }, [], false));
        var yo = factory.CrearCliente(31);

        for (var i = 1; i <= 20; i++)
            Assert.Equal(HttpStatusCode.OK, (await yo.PostAsJsonAsync("/api/assistant/conversations/3/messages", new { content = "hola" })).StatusCode);
        var vuelta21 = await yo.PostAsJsonAsync("/api/assistant/conversations/3/messages", new { content = "hola" });
        var otro = await factory.CrearCliente(32).PostAsJsonAsync("/api/assistant/conversations/3/messages", new { content = "hola" });

        Assert.Equal(HttpStatusCode.TooManyRequests, vuelta21.StatusCode);
        Assert.Equal("application/problem+json", vuelta21.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.OK, otro.StatusCode);
        // Solo el POST de mensajes lleva el límite: listar sigue respondiendo.
        factory.AssistantManager.Setup(m => m.ListConversationsAsync(31)).ReturnsAsync([]);
        Assert.Equal(HttpStatusCode.OK, (await yo.GetAsync("/api/assistant/conversations")).StatusCode);
    }
}
