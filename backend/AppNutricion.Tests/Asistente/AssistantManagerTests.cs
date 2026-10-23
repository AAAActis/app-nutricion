using System.Text.Json;
using AppNutricion.BusinessLogic.Exceptions;
using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.BusinessLogic.Managers;
using AppNutricion.BusinessLogic.Options;
using AppNutricion.BusinessLogic.Services;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.DataAccess.Stores;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;
using AppNutricion.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace AppNutricion.Tests.Asistente;

// S11: el asistente con FakeChatModel y con un IChatModel de Moq, sobre el AssistantStore y el MealEntryManager reales (SQLite).
public sealed class AssistantManagerTests : IDisposable
{
    private const int UserId = 4817;     // número raro a propósito: el prompt no lo puede contener
    private const int OtroId = 5923;
    private static readonly DateOnly Hoy = new(2026, 10, 22);

    private readonly BaseSqlite _db = new(UserId, OtroId);
    private readonly FakeTimeProvider _reloj = new(new DateTimeOffset(2026, 10, 22, 15, 0, 0, TimeSpan.Zero));
    private readonly Mock<IFoodManager> _foods = new();
    private readonly Mock<ISummaryManager> _summary = new();
    private readonly Mock<IGoalManager> _goals = new();
    private readonly Mock<IRoutineSummaryProvider> _routines = new();
    private readonly Mock<IChatModel> _modelo = new();
    private readonly List<(string Prompt, ChatTurn[] Turnos)> _llamadas = [];
    private readonly Food _arroz = new()
    {
        Name = "Arroz, grano, blanco, pulido, hervido", NormalizedName = "arroz grano blanco pulido hervido", Source = FoodSource.Argenfoods,
        ExternalId = "2", CaloriesPer100g = 82, ProteinPer100g = 1.4m, CarbsPer100g = 19.1m, FatPer100g = 0.1m,
        CreatedAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
    };
    private int _conversacion;
    private int _conversacionDeOtro;

    public AssistantManagerTests()
    {
        using (var context = _db.NuevoContexto())
        {
            context.Foods.Add(_arroz);
            var mia = new Conversation { UserId = UserId, CreatedAt = Ahora, UpdatedAt = Ahora };
            var deOtro = new Conversation { UserId = OtroId, CreatedAt = Ahora, UpdatedAt = Ahora };
            context.Conversations.AddRange(mia, deOtro);
            context.SaveChanges();
            _conversacion = mia.Id;
            _conversacionDeOtro = deOtro.Id;
        }
        _foods.Setup(f => f.SearchAsync(It.IsAny<string?>(), UserId, It.IsAny<int?>())).ReturnsAsync([_arroz]);
        _foods.Setup(f => f.GetByIdAsync(_arroz.Id, UserId)).ReturnsAsync(_arroz);
        _foods.Setup(f => f.ConvertToGrams(It.IsAny<Food>(), It.IsAny<decimal>(), false)).Returns((Food _, decimal q, bool _) => q);
        Totales(consumidas: 500, objetivo: 2000);
        _goals.Setup(g => g.ListAsync(UserId)).ReturnsAsync([new Goal { UserId = UserId, GoalType = GoalType.LoseFat, IsActive = true, TargetCalories = 2000 }]);
        _routines.Setup(r => r.GetAsync(UserId)).ReturnsAsync([new RoutineSummary("Torso/Pierna (4 días)", ["Torso A", "Pierna A"])]);
    }

    public void Dispose() => _db.Dispose();

    private DateTime Ahora => _reloj.GetUtcNow().UtcDateTime;

    private void Totales(decimal consumidas, decimal? objetivo)
    {
        var targets = objetivo is { } t ? new MacroTotals(t, 150, 200, 60) : null;
        var remaining = objetivo is { } r
            ? new RemainingMacros(new MacroLeft(r - consumidas, false), new MacroLeft(140, false), new MacroLeft(150, false), new MacroLeft(50, false))
            : null;
        _summary.Setup(s => s.GetDailyAsync(UserId, It.IsAny<DateOnly?>())).ReturnsAsync(new DailySummary(
            Hoy, new MacroTotals(consumidas, 10, 50, 10), [], targets, remaining, null));
    }

    private AssistantManager Manager(IChatModel modelo, double timeoutSegundos = 10)
    {
        var userToday = new UserToday(Mock.Of<IProfileStore>(), _reloj);
        var meals = new MealEntryManager(new MealEntryStore(_db.NuevoContexto()), _foods.Object, userToday, _reloj);
        var tools = new AssistantTools(_foods.Object, _summary.Object, _routines.Object, userToday);
        return new AssistantManager(new AssistantStore(_db.NuevoContexto()), modelo, tools, meals, _summary.Object, _goals.Object,
            _routines.Object, new LlmOptions { TimeoutSeconds = timeoutSegundos }, _reloj);
    }

    private AssistantManager ConFake() => Manager(new FakeChatModel());

    // El mock responde en orden lo que se le pase y guarda prompt y turnos de cada llamada.
    private AssistantManager ConMock(params ChatCompletion[] respuestas)
    {
        var cola = new Queue<ChatCompletion>(respuestas);
        _modelo.Setup(m => m.CompleteAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ChatTurn>>(), It.IsAny<IReadOnlyList<ChatToolDefinition>>(), It.IsAny<CancellationToken>()))
            .Callback((string p, IReadOnlyList<ChatTurn> t, IReadOnlyList<ChatToolDefinition> _, CancellationToken _) => _llamadas.Add((p, t.ToArray())))
            .ReturnsAsync(() => cola.Count > 1 ? cola.Dequeue() : cola.Peek());
        return Manager(_modelo.Object);
    }

    private static ChatCompletion Texto(string texto) => new(texto, []);

    private static ChatCompletion Herramienta(string nombre, object argumentos, string id = "c1") =>
        new(null, [new ChatToolCall(id, nombre, JsonSerializer.Serialize(argumentos))]);

    private async Task<List<MealEntry>> Comidas()
    {
        await using var context = _db.NuevoContexto();
        return await context.MealEntries.ToListAsync();
    }

    private async Task<List<ChatMessage>> Mensajes()
    {
        await using var context = _db.NuevoContexto();
        return await context.ChatMessages.Where(m => m.ConversationId == _conversacion).OrderBy(m => m.Id).ToListAsync();
    }

    private async Task<List<AssistantSuggestion>> Sugerencias()
    {
        await using var context = _db.NuevoContexto();
        return await context.AssistantSuggestions.ToListAsync();
    }

    // ---- Flujo con FakeChatModel ----

    [Fact]
    public async Task ComiArroz_CreaUnaSugerenciaPendiente_YNingunaComida()
    {
        var reply = await ConFake().SendAsync(UserId, _conversacion, "comí 150 g de arroz");

        Assert.NotNull(reply);
        Assert.False(reply.Degraded);
        var s = Assert.Single(reply.Suggestions);
        Assert.Equal((UserId, _arroz.Id, 150m, MealType.Lunch, Hoy, SuggestionStatus.Pending), (s.UserId, s.FoodId, s.QuantityGrams, s.MealType, s.Date, s.Status));
        Assert.Equal(reply.Message.Id, s.ChatMessageId);
        Assert.Equal(Ahora, s.CreatedAt);
        Assert.Contains("150 g", reply.Message.Content);
        Assert.Empty(await Comidas());
        Assert.Single(await Sugerencias());
        Assert.Equal([ChatRole.User, ChatRole.Tool, ChatRole.Tool, ChatRole.Assistant], (await Mensajes()).Select(m => m.Role));
        _foods.Verify(f => f.SearchAsync("arroz", UserId, 5), Times.Once);
    }

    [Fact]
    public async Task LasSugerenciasDeLaRespuesta_TraenElAlimento()
    {
        var reply = await ConFake().SendAsync(UserId, _conversacion, "comí 150 g de arroz");

        Assert.Equal(_arroz.Name, Assert.Single(reply!.Suggestions).Food?.Name);
    }

    [Fact]
    public async Task ComiSinTilde_TambienPropone()
    {
        var reply = await ConFake().SendAsync(UserId, _conversacion, "comi 200g de arroz");

        Assert.Equal(200m, Assert.Single(reply!.Suggestions).QuantityGrams);
    }

    [Fact]
    public async Task CuantoMeFalta_UsaLosTotalesDelDia()
    {
        var reply = await ConFake().SendAsync(UserId, _conversacion, "¿Cuánto me falta hoy?");

        Assert.Contains("500", reply!.Message.Content);
        Assert.Contains("1500", reply.Message.Content);
        Assert.Empty(reply.Suggestions);
        _summary.Verify(s => s.GetDailyAsync(UserId, It.IsAny<DateOnly?>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task OtraCosa_RespuestaGenericaSinHerramientas()
    {
        var reply = await ConFake().SendAsync(UserId, _conversacion, "hola");

        Assert.False(string.IsNullOrWhiteSpace(reply!.Message.Content));
        Assert.Equal([ChatRole.User, ChatRole.Assistant], (await Mensajes()).Select(m => m.Role));
    }

    // ---- Aceptar e ignorar ----

    [Fact]
    public async Task Aceptar_CreaLaComidaConLosMacrosDeFoods_NoLosDelLlm()
    {
        var manager = ConMock(
            Herramienta(AssistantTools.ProposeIntake, new { foodId = _arroz.Id, gramos = 150, mealType = "Lunch", kcal = 9999, proteinas = 999 }),
            Texto("Listo, confirmala."));
        var sugerencia = Assert.Single((await manager.SendAsync(UserId, _conversacion, "comí arroz"))!.Suggestions);

        var aceptada = await manager.AcceptSuggestionAsync(sugerencia.Id, UserId);

        var comida = Assert.Single(await Comidas());
        Assert.Equal((150m, 123m, 2.1m, 28.65m, 0.15m), (comida.QuantityGrams, comida.SavedCalories, comida.SavedProtein, comida.SavedCarbs, comida.SavedFat));
        Assert.Equal((UserId, _arroz.Id, MealType.Lunch, Hoy), (comida.UserId, comida.FoodId, comida.MealType, comida.Date));
        Assert.NotNull(aceptada);
        Assert.Equal((SuggestionStatus.Accepted, (int?)comida.Id, (DateTime?)Ahora), (aceptada.Status, aceptada.MealEntryId, aceptada.ResolvedAt));
        var guardada = Assert.Single(await Sugerencias());
        Assert.Equal((SuggestionStatus.Accepted, (int?)comida.Id), (guardada.Status, guardada.MealEntryId));
    }

    [Fact]
    public async Task Ignorar_NoCreaNada()
    {
        var manager = ConFake();
        var sugerencia = Assert.Single((await manager.SendAsync(UserId, _conversacion, "comí 150 g de arroz"))!.Suggestions);

        var ignorada = await manager.IgnoreSuggestionAsync(sugerencia.Id, UserId);

        Assert.Equal((SuggestionStatus.Ignored, (DateTime?)Ahora), (ignorada!.Status, ignorada.ResolvedAt));
        Assert.Empty(await Comidas());
        Assert.Equal(SuggestionStatus.Ignored, Assert.Single(await Sugerencias()).Status);
    }

    [Fact]
    public async Task AceptarDosVeces_Conflict_YUnaSolaComida()
    {
        var manager = ConFake();
        var sugerencia = Assert.Single((await manager.SendAsync(UserId, _conversacion, "comí 150 g de arroz"))!.Suggestions);
        await manager.AcceptSuggestionAsync(sugerencia.Id, UserId);

        await Assert.ThrowsAsync<ConflictException>(() => manager.AcceptSuggestionAsync(sugerencia.Id, UserId));
        await Assert.ThrowsAsync<ConflictException>(() => manager.IgnoreSuggestionAsync(sugerencia.Id, UserId));
        Assert.Single(await Comidas());
    }

    [Fact]
    public async Task IgnorarYDespuesAceptar_Conflict()
    {
        var manager = ConFake();
        var sugerencia = Assert.Single((await manager.SendAsync(UserId, _conversacion, "comí 150 g de arroz"))!.Suggestions);
        await manager.IgnoreSuggestionAsync(sugerencia.Id, UserId);

        await Assert.ThrowsAsync<ConflictException>(() => manager.AcceptSuggestionAsync(sugerencia.Id, UserId));
        Assert.Empty(await Comidas());
    }

    // La guarda va antes de MealEntryManager: una ya resuelta da "ya fue resuelta" aunque el alimento ya no esté
    // o la fecha haya quedado fuera del diario, y no llega a buscar el alimento (no crea ni borra una comida).
    [Fact]
    public async Task AceptarUnaYaResuelta_ConflictYaResuelta_SinTocarElAlimento()
    {
        var manager = ConFake();
        var sugerencia = Assert.Single((await manager.SendAsync(UserId, _conversacion, "comí 150 g de arroz"))!.Suggestions);
        await manager.IgnoreSuggestionAsync(sugerencia.Id, UserId);
        _foods.Invocations.Clear();
        _foods.Setup(f => f.GetByIdAsync(_arroz.Id, UserId)).ReturnsAsync((Food?)null);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => manager.AcceptSuggestionAsync(sugerencia.Id, UserId));

        Assert.Equal("La sugerencia ya fue resuelta.", ex.Message);
        _foods.Verify(f => f.GetByIdAsync(It.IsAny<int>(), It.IsAny<int?>()), Times.Never);
        Assert.Empty(await Comidas());
    }

    [Fact]
    public async Task AceptarUnaYaResuelta_ConLaFechaFueraDelDiario_SigueSiendoConflict()
    {
        var manager = ConFake();
        var sugerencia = Assert.Single((await manager.SendAsync(UserId, _conversacion, "comí 150 g de arroz"))!.Suggestions);
        await manager.IgnoreSuggestionAsync(sugerencia.Id, UserId);
        _reloj.Advance(TimeSpan.FromDays(3 * 365));

        await Assert.ThrowsAsync<ConflictException>(() => manager.AcceptSuggestionAsync(sugerencia.Id, UserId));
    }

    [Fact]
    public async Task SugerenciaDeOtroUsuario_Null()
    {
        var manager = ConFake();
        var sugerencia = Assert.Single((await manager.SendAsync(UserId, _conversacion, "comí 150 g de arroz"))!.Suggestions);

        Assert.Null(await manager.AcceptSuggestionAsync(sugerencia.Id, OtroId));
        Assert.Null(await manager.IgnoreSuggestionAsync(sugerencia.Id, OtroId));
        Assert.Null(await manager.AcceptSuggestionAsync(999, UserId));
        Assert.Equal(SuggestionStatus.Pending, Assert.Single(await Sugerencias()).Status);
    }

    [Fact]
    public async Task AceptarConElAlimentoQueYaNoEsta_ConflictYSigueDisponible()
    {
        var manager = ConFake();
        var sugerencia = Assert.Single((await manager.SendAsync(UserId, _conversacion, "comí 150 g de arroz"))!.Suggestions);
        _foods.Setup(f => f.GetByIdAsync(_arroz.Id, UserId)).ReturnsAsync((Food?)null);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => manager.AcceptSuggestionAsync(sugerencia.Id, UserId));

        Assert.Equal("El alimento ya no está disponible", ex.Message);
        Assert.Empty(await Comidas());
        Assert.Equal(SuggestionStatus.Pending, Assert.Single(await Sugerencias()).Status);
    }

    // ---- Conversaciones ----

    [Fact]
    public async Task ConversacionDeOtroUsuario_NullEnTodo()
    {
        var manager = ConFake();

        Assert.Null(await manager.SendAsync(UserId, _conversacionDeOtro, "comí 150 g de arroz"));
        Assert.Null(await manager.GetHistoryAsync(_conversacionDeOtro, UserId));
        Assert.Empty(await Sugerencias());
    }

    [Fact]
    public async Task CrearYListarConversaciones()
    {
        var manager = ConFake();
        _reloj.Advance(TimeSpan.FromMinutes(5));

        var nueva = await manager.CreateConversationAsync(UserId);

        Assert.Equal((UserId, (string?)null, Ahora, Ahora), (nueva.UserId, nueva.Title, nueva.CreatedAt, nueva.UpdatedAt));
        Assert.Equal([nueva.Id, _conversacion], (await manager.ListConversationsAsync(UserId)).Select(c => c.Id));
    }

    [Fact]
    public async Task Enviar_PoneTituloConLos60PrimerosCaracteres_YActualizaUpdatedAt()
    {
        var texto = "comí 150 g de arroz " + new string('x', 80);
        _reloj.Advance(TimeSpan.FromMinutes(10));

        await ConFake().SendAsync(UserId, _conversacion, texto);
        _reloj.Advance(TimeSpan.FromMinutes(1));
        await ConFake().SendAsync(UserId, _conversacion, "hola");

        await using var context = _db.NuevoContexto();
        var c = await context.Conversations.SingleAsync(x => x.Id == _conversacion);
        Assert.Equal(texto[..60], c.Title);
        Assert.Equal(Ahora, c.UpdatedAt);
    }

    [Fact]
    public async Task Historial_MensajesSinTool_ConSusSugerencias()
    {
        var manager = ConFake();
        var reply = await manager.SendAsync(UserId, _conversacion, "comí 150 g de arroz");

        var historial = await manager.GetHistoryAsync(_conversacion, UserId);

        Assert.Equal([ChatRole.User, ChatRole.Assistant], historial!.Messages.Select(m => m.Role));
        Assert.Equal(reply!.Message.Id, Assert.Single(historial.Suggestions).ChatMessageId);
    }

    // ---- Privacidad y contexto ----

    [Fact]
    public async Task ElModeloNoRecibeUserIdNiEmail_YSiElContextoPermitido()
    {
        await ConMock(Texto("ok")).SendAsync(UserId, _conversacion, "hola");

        var (prompt, turnos) = Assert.Single(_llamadas);
        var todo = prompt + string.Join("\n", turnos.Select(t => t.Content));
        Assert.DoesNotContain(UserId.ToString(), todo);
        Assert.DoesNotContain("@", todo);
        Assert.Contains("2000", prompt);              // objetivo
        Assert.Contains("500", prompt);               // consumido hoy
        Assert.Contains("LoseFat", prompt);           // tipo de objetivo
        Assert.Contains("Torso/Pierna (4 días)", prompt);
        Assert.Contains("rioplatense", prompt);
        Assert.Contains("18", prompt);
        Assert.Contains("profesional", prompt);
        Assert.Contains("estimación", prompt);
    }

    [Fact]
    public async Task ElModeloRecibeLosUltimos20Mensajes_ConElNuevoAlFinal()
    {
        await using (var context = _db.NuevoContexto())
        {
            context.ChatMessages.AddRange(Enumerable.Range(1, 25).Select(i => new ChatMessage
            {
                ConversationId = _conversacion, Role = i % 2 == 1 ? ChatRole.User : ChatRole.Assistant, Content = $"m{i}",
                CreatedAt = Ahora.AddMinutes(-30 + i),
            }));
            await context.SaveChangesAsync();
        }

        await ConMock(Texto("ok")).SendAsync(UserId, _conversacion, "nuevo");

        var turnos = Assert.Single(_llamadas).Turnos;
        Assert.Equal(20, turnos.Length);
        Assert.Equal("m7", turnos[0].Content);
        Assert.Equal((ChatRole.User, "nuevo"), (turnos[^1].Role, turnos[^1].Content));
    }

    [Fact]
    public async Task ElUserIdQueMandaElLlm_SeIgnora()
    {
        var reply = await ConMock(
            Herramienta(AssistantTools.ProposeIntake, new { foodId = _arroz.Id, gramos = 100, mealType = "Dinner", userId = OtroId }),
            Texto("ok")).SendAsync(UserId, _conversacion, "comí arroz");

        Assert.Equal(UserId, Assert.Single(reply!.Suggestions).UserId);
    }

    // ---- Fallback ----

    [Fact]
    public async Task ElModeloTiraExcepcion_DegradedConLosTotales()
    {
        _modelo.Setup(m => m.CompleteAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ChatTurn>>(), It.IsAny<IReadOnlyList<ChatToolDefinition>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("caído"));

        var reply = await Manager(_modelo.Object).SendAsync(UserId, _conversacion, "comí 150 g de arroz");

        Assert.True(reply!.Degraded);
        Assert.Equal("El asistente no está disponible en este momento. Hoy llevás 500 kcal de 2000.", reply.Message.Content);
        Assert.Empty(reply.Suggestions);
        Assert.Equal([ChatRole.User, ChatRole.Assistant], (await Mensajes()).Select(m => m.Role));
        Assert.Equal(reply.Message.Content, (await Mensajes()).Last().Content);
    }

    [Fact]
    public async Task ElModeloFalla_SinObjetivo_SoloLoConsumido()
    {
        Totales(consumidas: 730, objetivo: null);
        _modelo.Setup(m => m.CompleteAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ChatTurn>>(), It.IsAny<IReadOnlyList<ChatToolDefinition>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException());

        var reply = await Manager(_modelo.Object).SendAsync(UserId, _conversacion, "hola");

        Assert.Equal("El asistente no está disponible en este momento. Hoy llevás 730 kcal.", reply!.Message.Content);
    }

    [Fact]
    public async Task ElModeloTardaMasQueElTimeout_DegradedYSeCancelaElToken()
    {
        var nunca = new TaskCompletionSource<ChatCompletion>();
        CancellationToken recibido = default;
        _modelo.Setup(m => m.CompleteAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ChatTurn>>(), It.IsAny<IReadOnlyList<ChatToolDefinition>>(), It.IsAny<CancellationToken>()))
            .Callback((string _, IReadOnlyList<ChatTurn> _, IReadOnlyList<ChatToolDefinition> _, CancellationToken ct) => recibido = ct)
            .Returns(nunca.Task);

        var envio = Manager(_modelo.Object, timeoutSegundos: 10).SendAsync(UserId, _conversacion, "hola");
        _reloj.Advance(TimeSpan.FromSeconds(9));
        Assert.False(envio.IsCompleted);
        _reloj.Advance(TimeSpan.FromSeconds(2));
        var reply = await envio.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(reply!.Degraded);
        Assert.StartsWith("El asistente no está disponible", reply.Message.Content);
        Assert.True(recibido.IsCancellationRequested);
    }

    // ---- Guardrails ----

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task TextoVacio_ArgumentExceptionContent(string? texto)
    {
        var ex = await Assert.ThrowsAnyAsync<ArgumentException>(() => ConFake().SendAsync(UserId, _conversacion, texto));

        Assert.Equal("content", ex.ParamName);
        Assert.Empty(await Mensajes());
    }

    [Fact]
    public async Task TextoDeHasta1000Caracteres()
    {
        var ex = await Assert.ThrowsAnyAsync<ArgumentException>(() => ConFake().SendAsync(UserId, _conversacion, new string('a', 1001)));
        Assert.Equal("content", ex.ParamName);

        Assert.NotNull(await ConFake().SendAsync(UserId, _conversacion, new string('a', 1000)));
    }

    public static TheoryData<string> PropuestasInvalidas => new()
    {
        """{"foodId": 1, "gramos": 0, "mealType": "Lunch"}""",
        """{"foodId": 1, "gramos": 5001, "mealType": "Lunch"}""",
        """{"foodId": 999, "gramos": 100, "mealType": "Lunch"}""",
        """{"foodId": 1, "gramos": 100, "mealType": "Merienda"}""",
        """{"foodId": 1, "gramos": 100, "mealType": "Lunch", "fecha": "2026-10-25"}""",
        """{"foodId": 1, "gramos": 100, "mealType": "Lunch", "fecha": "ayer"}""",
        """{"foodId": "uno", "gramos": 100, "mealType": "Lunch"}""",
        """{"gramos": 100, "mealType": "Lunch"}""",
        """no es json""",
    };

    [Theory]
    [MemberData(nameof(PropuestasInvalidas))]
    public async Task ProponerIngestaInvalida_DevuelveErrorAlModelo_YNoCreaSugerencia(string argumentos)
    {
        argumentos = argumentos.Replace("\"foodId\": 1,", $"\"foodId\": {_arroz.Id},");
        var reply = await ConMock(new ChatCompletion(null, [new ChatToolCall("c1", AssistantTools.ProposeIntake, argumentos)]), Texto("No pude."))
            .SendAsync(UserId, _conversacion, "comí arroz");

        Assert.Empty(reply!.Suggestions);
        Assert.Empty(await Sugerencias());
        var resultado = _llamadas[1].Turnos.Last();
        Assert.Equal((ChatRole.Tool, "c1"), (resultado.Role, resultado.ToolCallId));
        Assert.Contains("\"error\"", resultado.Content);
    }

    [Fact]
    public async Task ProponerIngestaEnLosLimitesYConFecha_EsValida()
    {
        var reply = await ConMock(
            new ChatCompletion(null,
            [
                new ChatToolCall("a", AssistantTools.ProposeIntake, JsonSerializer.Serialize(new { foodId = _arroz.Id, gramos = 0.01, mealType = "breakfast" })),
                new ChatToolCall("b", AssistantTools.ProposeIntake, JsonSerializer.Serialize(new { foodId = _arroz.Id, gramos = 5000, mealType = "Dinner", fecha = "2026-10-21" })),
            ]),
            Texto("ok")).SendAsync(UserId, _conversacion, "comí arroz");

        Assert.Equal([(0.01m, MealType.Breakfast, Hoy), (5000m, MealType.Dinner, Hoy.AddDays(-1))],
            reply!.Suggestions.Select(s => (s.QuantityGrams, s.MealType, s.Date)));
    }

    [Fact]
    public async Task HerramientaDesconocida_ErrorAlModelo()
    {
        await ConMock(Herramienta("borrar_todo", new { }), Texto("ok")).SendAsync(UserId, _conversacion, "hola");

        Assert.Contains("\"error\"", _llamadas[1].Turnos.Last().Content);
    }

    [Fact]
    public async Task MasDeTresVueltasDeHerramientas_SeCortaYResponde()
    {
        var reply = await ConMock(Herramienta(AssistantTools.SearchFood, new { q = "arroz" }))   // siempre pide otra herramienta
            .SendAsync(UserId, _conversacion, "comí arroz");

        Assert.Equal(4, _llamadas.Count);                                   // 3 vueltas con herramientas + la que se corta
        _foods.Verify(f => f.SearchAsync(It.IsAny<string?>(), UserId, It.IsAny<int?>()), Times.Exactly(3));
        Assert.False(reply!.Degraded);
        Assert.False(string.IsNullOrWhiteSpace(reply.Message.Content));
        Assert.Equal(3, (await Mensajes()).Count(m => m.Role == ChatRole.Tool));
    }

    // ---- Herramientas ----

    [Fact]
    public async Task BuscarAlimento_HastaCincoResultadosConMacrosPor100g()
    {
        var muchos = Enumerable.Range(1, 7).Select(i => new Food { Id = 100 + i, Name = $"Arroz {i}", CaloriesPer100g = 80 + i, ProteinPer100g = 1, CarbsPer100g = 20, FatPer100g = 0.5m }).ToList();
        _foods.Setup(f => f.SearchAsync("arroz", UserId, 5)).ReturnsAsync(muchos.Take(5).ToList());

        await ConMock(Herramienta(AssistantTools.SearchFood, new { q = "arroz" }), Texto("ok")).SendAsync(UserId, _conversacion, "hola");

        var json = JsonDocument.Parse(_llamadas[1].Turnos.Last().Content).RootElement.GetProperty("resultados");
        Assert.Equal(5, json.GetArrayLength());
        Assert.Equal((101, "Arroz 1", 81m), (json[0].GetProperty("id").GetInt32(), json[0].GetProperty("nombre").GetString(), json[0].GetProperty("kcal").GetDecimal()));
        Assert.True(json[0].TryGetProperty("proteinas", out _));
        _foods.Verify(f => f.SearchAsync("arroz", UserId, 5), Times.Once);
    }

    [Fact]
    public async Task TotalesDelDia_ConFechaInvalida_Error()
    {
        await ConMock(Herramienta(AssistantTools.DailyTotals, new { fecha = "mañana" }), Texto("ok")).SendAsync(UserId, _conversacion, "hola");

        Assert.Contains("\"error\"", _llamadas[1].Turnos.Last().Content);
    }

    [Fact]
    public async Task ConsultarRutinas_NombresYDias()
    {
        await ConMock(Herramienta(AssistantTools.ListRoutines, new { }), Texto("ok")).SendAsync(UserId, _conversacion, "hola");

        var resultado = _llamadas[1].Turnos.Last().Content;
        Assert.Contains("Torso/Pierna (4 días)", resultado);
        Assert.Contains("Pierna A", resultado);
    }

    [Fact]
    public async Task LasCuatroHerramientasSeOfrecenAlModelo()
    {
        IReadOnlyList<ChatToolDefinition>? ofrecidas = null;
        _modelo.Setup(m => m.CompleteAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ChatTurn>>(), It.IsAny<IReadOnlyList<ChatToolDefinition>>(), It.IsAny<CancellationToken>()))
            .Callback((string _, IReadOnlyList<ChatTurn> _, IReadOnlyList<ChatToolDefinition> t, CancellationToken _) => ofrecidas = t)
            .ReturnsAsync(Texto("ok"));

        await Manager(_modelo.Object).SendAsync(UserId, _conversacion, "hola");

        Assert.Equal(["buscar_alimento", "totales_del_dia", "consultar_rutinas", "proponer_ingesta"], ofrecidas!.Select(t => t.Name));
        Assert.All(ofrecidas!, t => JsonDocument.Parse(t.ParametersJsonSchema));
    }
}
