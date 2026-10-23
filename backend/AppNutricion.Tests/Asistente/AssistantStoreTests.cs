using AppNutricion.DataAccess.Stores;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;
using AppNutricion.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;

namespace AppNutricion.Tests.Asistente;

public sealed class AssistantStoreTests : IDisposable
{
    private const int Yo = 1;
    private const int Otro = 2;
    private static readonly DateTime Base = new(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Dia = new(2026, 10, 22);

    private readonly BaseSqlite _db = new(Yo, Otro);
    private readonly Food _arroz = new()
    {
        Name = "Arroz blanco hervido", NormalizedName = "arroz blanco hervido", Source = FoodSource.Argenfoods, ExternalId = "2",
        CaloriesPer100g = 82, ProteinPer100g = 1.4m, CarbsPer100g = 19.1m, FatPer100g = 0.1m, CreatedAt = Base,
    };

    public AssistantStoreTests()
    {
        using var context = _db.NuevoContexto();
        context.Foods.Add(_arroz);
        context.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private AssistantStore NuevoStore() => new(_db.NuevoContexto());

    private async Task<Conversation> Conversacion(int userId, int minutos = 0, string? titulo = null, params (ChatRole Role, string Texto)[] mensajes)
    {
        var conversation = new Conversation
        {
            UserId = userId, Title = titulo, CreatedAt = Base, UpdatedAt = Base.AddMinutes(minutos),
            Messages = mensajes.Select((m, i) => new ChatMessage { Role = m.Role, Content = m.Texto, CreatedAt = Base.AddSeconds(i) }).ToList(),
        };
        await using var context = _db.NuevoContexto();
        context.Conversations.Add(conversation);
        await context.SaveChangesAsync();
        return conversation;
    }

    private async Task<AssistantSuggestion> Sugerencia(int userId, int chatMessageId, SuggestionStatus status = SuggestionStatus.Pending, int? mealEntryId = null)
    {
        var suggestion = new AssistantSuggestion
        {
            UserId = userId, ChatMessageId = chatMessageId, FoodId = _arroz.Id, QuantityGrams = 150, MealType = MealType.Lunch,
            Date = Dia, Status = status, CreatedAt = Base, MealEntryId = mealEntryId,
        };
        await using var context = _db.NuevoContexto();
        context.AssistantSuggestions.Add(suggestion);
        await context.SaveChangesAsync();
        return suggestion;
    }

    // ---- Conversaciones ----

    [Fact]
    public async Task Conversaciones_DelUsuario_MasRecientePrimero()
    {
        var vieja = await Conversacion(Yo, minutos: 1, titulo: "vieja");
        var nueva = await Conversacion(Yo, minutos: 5, titulo: "nueva");
        await Conversacion(Otro, minutos: 9, titulo: "de otro");

        var lista = await NuevoStore().ListConversationsAsync(Yo);

        Assert.Equal([nueva.Id, vieja.Id], lista.Select(c => c.Id));
    }

    [Fact]
    public async Task Conversaciones_AddYGet_DeOtroNull()
    {
        var conversation = new Conversation { UserId = Yo, CreatedAt = Base, UpdatedAt = Base };
        await NuevoStore().AddConversationAsync(conversation);
        var store = NuevoStore();

        Assert.True(conversation.Id > 0);
        Assert.NotNull(await store.GetConversationAsync(conversation.Id, Yo));
        Assert.Null(await store.GetConversationAsync(conversation.Id, Otro));
    }

    [Fact]
    public async Task Touch_PoneTituloSoloSiNoTiene_YActualizaUpdatedAt()
    {
        var sinTitulo = await Conversacion(Yo);
        var conTitulo = await Conversacion(Yo, titulo: "Original");
        var store = NuevoStore();

        await store.TouchConversationAsync(sinTitulo.Id, Yo, "Comí arroz", Base.AddHours(1));
        await store.TouchConversationAsync(conTitulo.Id, Yo, "Otro título", Base.AddHours(2));
        await store.TouchConversationAsync(conTitulo.Id, Otro, "De otro", Base.AddHours(3));

        await using var context = _db.NuevoContexto();
        var a = await context.Conversations.SingleAsync(c => c.Id == sinTitulo.Id);
        var b = await context.Conversations.SingleAsync(c => c.Id == conTitulo.Id);
        Assert.Equal(("Comí arroz", Base.AddHours(1)), (a.Title, a.UpdatedAt));
        Assert.Equal(("Original", Base.AddHours(2)), (b.Title, b.UpdatedAt));
    }

    // ---- Mensajes ----

    [Fact]
    public async Task MensajesRecientes_UltimosNUserYAssistantEnOrden_SinTool()
    {
        var c = await Conversacion(Yo, 0, null,
            (ChatRole.User, "u1"), (ChatRole.Assistant, "a1"), (ChatRole.User, "u2"), (ChatRole.Tool, "t1"), (ChatRole.Assistant, "a2"));

        var recientes = await NuevoStore().GetRecentMessagesAsync(c.Id, Yo, 3);

        Assert.Equal(["a1", "u2", "a2"], recientes.Select(m => m.Content));
    }

    [Fact]
    public async Task Mensajes_DeConversacionDeOtro_Vacio()
    {
        var c = await Conversacion(Otro, 0, null, (ChatRole.User, "u1"));
        var store = NuevoStore();

        Assert.Empty(await store.GetRecentMessagesAsync(c.Id, Yo, 20));
        Assert.Empty(await store.GetVisibleMessagesAsync(c.Id, Yo));
    }

    [Fact]
    public async Task MensajesVisibles_TodosLosUserYAssistant_SinTool()
    {
        var c = await Conversacion(Yo, 0, null, (ChatRole.User, "u1"), (ChatRole.Tool, "t1"), (ChatRole.Assistant, "a1"));

        Assert.Equal(["u1", "a1"], (await NuevoStore().GetVisibleMessagesAsync(c.Id, Yo)).Select(m => m.Content));
    }

    [Fact]
    public async Task AddMessages_LosGuarda()
    {
        var c = await Conversacion(Yo);

        await NuevoStore().AddMessagesAsync([
            new ChatMessage { ConversationId = c.Id, Role = ChatRole.User, Content = "hola", CreatedAt = Base },
            new ChatMessage { ConversationId = c.Id, Role = ChatRole.Tool, Content = "tool", CreatedAt = Base.AddSeconds(1) },
        ]);

        await using var context = _db.NuevoContexto();
        Assert.Equal(2, await context.ChatMessages.CountAsync(m => m.ConversationId == c.Id));
    }

    // ---- Sugerencias ----

    [Fact]
    public async Task Sugerencias_DeLaConversacion_ConSuAlimento()
    {
        var mia = await Conversacion(Yo, 0, null, (ChatRole.Assistant, "a1"));
        var otra = await Conversacion(Yo, 0, null, (ChatRole.Assistant, "a2"));
        var s = await Sugerencia(Yo, mia.Messages.First().Id);
        await Sugerencia(Yo, otra.Messages.First().Id);

        var lista = await NuevoStore().GetSuggestionsAsync(mia.Id, Yo);

        Assert.Equal(s.Id, Assert.Single(lista).Id);
        Assert.Equal("Arroz blanco hervido", lista[0].Food.Name);
        Assert.Empty(await NuevoStore().GetSuggestionsAsync(mia.Id, Otro));
    }

    [Fact]
    public async Task Sugerencias_AddYGet_ConAlimento_DeOtroNull()
    {
        var c = await Conversacion(Yo, 0, null, (ChatRole.Assistant, "a1"));
        var nueva = new AssistantSuggestion
        {
            UserId = Yo, ChatMessageId = c.Messages.First().Id, FoodId = _arroz.Id, QuantityGrams = 150, MealType = MealType.Lunch,
            Date = Dia, Status = SuggestionStatus.Pending, CreatedAt = Base,
        };
        await NuevoStore().AddSuggestionsAsync([nueva]);
        var store = NuevoStore();

        var leida = await store.GetSuggestionAsync(nueva.Id, Yo);
        Assert.NotNull(leida);
        Assert.Equal(("Arroz blanco hervido", 150m, SuggestionStatus.Pending), (leida.Food.Name, leida.QuantityGrams, leida.Status));
        Assert.Null(await store.GetSuggestionAsync(nueva.Id, Otro));
    }

    [Fact]
    public async Task Resolve_SoloSiSiguePendiente_YSoloLaPropia()
    {
        var c = await Conversacion(Yo, 0, null, (ChatRole.Assistant, "a1"));
        var s = await Sugerencia(Yo, c.Messages.First().Id);
        var store = NuevoStore();

        Assert.False(await store.ResolveSuggestionAsync(s.Id, Otro, SuggestionStatus.Ignored, Base.AddHours(1), null));
        Assert.True(await store.ResolveSuggestionAsync(s.Id, Yo, SuggestionStatus.Ignored, Base.AddHours(1), null));
        Assert.False(await store.ResolveSuggestionAsync(s.Id, Yo, SuggestionStatus.Accepted, Base.AddHours(2), null));

        await using var context = _db.NuevoContexto();
        var guardada = await context.AssistantSuggestions.SingleAsync(x => x.Id == s.Id);
        Assert.Equal((SuggestionStatus.Ignored, (DateTime?)Base.AddHours(1)), (guardada.Status, guardada.ResolvedAt));
    }

    [Fact]
    public async Task Resolve_Aceptada_GuardaElMealEntryId()
    {
        var c = await Conversacion(Yo, 0, null, (ChatRole.Assistant, "a1"));
        var s = await Sugerencia(Yo, c.Messages.First().Id);
        int comida;
        await using (var context = _db.NuevoContexto())
        {
            var entry = new MealEntry { UserId = Yo, FoodId = _arroz.Id, ItemName = "Arroz", Date = Dia, MealType = MealType.Lunch, QuantityGrams = 150, SavedCalories = 123, CreatedAt = Base };
            context.MealEntries.Add(entry);
            await context.SaveChangesAsync();
            comida = entry.Id;
        }

        Assert.True(await NuevoStore().ResolveSuggestionAsync(s.Id, Yo, SuggestionStatus.Accepted, Base.AddHours(1), comida));

        await using var verificar = _db.NuevoContexto();
        Assert.Equal(comida, (await verificar.AssistantSuggestions.SingleAsync(x => x.Id == s.Id)).MealEntryId);
    }

    // ---- J5: export y borrado de cuenta ----

    [Fact]
    public async Task Export_IncluyeLasSugerenciasDelUsuario_NuncaLasDeOtro()
    {
        var mia = await Conversacion(Yo, 0, null, (ChatRole.Assistant, "a1"));
        var deOtro = await Conversacion(Otro, 0, null, (ChatRole.Assistant, "a2"));
        var s = await Sugerencia(Yo, mia.Messages.First().Id, SuggestionStatus.Ignored);
        await Sugerencia(Otro, deOtro.Messages.First().Id);

        var export = await new ExportStore(_db.NuevoContexto()).GetAsync(Yo);

        var exportada = Assert.Single(export!.AssistantSuggestions);
        Assert.Equal((s.Id, SuggestionStatus.Ignored, "Arroz blanco hervido"), (exportada.Id, exportada.Status, exportada.Food.Name));
    }

    [Fact]
    public async Task Export_IncluyeLosMensajesTool()
    {
        await Conversacion(Yo, 0, null, (ChatRole.User, "u1"), (ChatRole.Tool, "t1"), (ChatRole.Assistant, "a1"));

        var export = await new ExportStore(_db.NuevoContexto()).GetAsync(Yo);

        Assert.Equal(["u1", "t1", "a1"], Assert.Single(export!.Conversations).Messages.Select(m => m.Content));
    }

    // Lo que hace DELETE /api/me (AccountManager → UserAccountStore.DeleteWithDataAsync).
    [Fact]
    public async Task BorrarCuenta_BorraConversacionesMensajesYSugerencias_YNoTocaLasDeOtro()
    {
        var mia = await Conversacion(Yo, 0, null, (ChatRole.User, "u1"), (ChatRole.Tool, "t1"), (ChatRole.Assistant, "a1"));
        var deOtro = await Conversacion(Otro, 0, null, (ChatRole.User, "u2"), (ChatRole.Assistant, "a2"));
        int comida;
        await using (var context = _db.NuevoContexto())
        {
            var entry = new MealEntry { UserId = Yo, FoodId = _arroz.Id, ItemName = "Arroz", Date = Dia, MealType = MealType.Lunch, QuantityGrams = 150, SavedCalories = 123, CreatedAt = Base };
            context.MealEntries.Add(entry);
            await context.SaveChangesAsync();
            comida = entry.Id;
        }
        await Sugerencia(Yo, mia.Messages.Last().Id, SuggestionStatus.Accepted, comida);
        await Sugerencia(Yo, mia.Messages.Last().Id);
        var suya = await Sugerencia(Otro, deOtro.Messages.Last().Id);

        await new UserAccountStore(_db.NuevoContexto()).DeleteWithDataAsync(Yo);

        await using var verificar = _db.NuevoContexto();
        Assert.Equal([deOtro.Id], await verificar.Conversations.Select(c => c.Id).ToListAsync());
        Assert.Equal(["u2", "a2"], await verificar.ChatMessages.OrderBy(m => m.Id).Select(m => m.Content).ToListAsync());
        Assert.Equal([suya.Id], await verificar.AssistantSuggestions.Select(s => s.Id).ToListAsync());
    }
}
