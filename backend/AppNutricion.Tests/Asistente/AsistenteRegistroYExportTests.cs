using System.Net;
using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.BusinessLogic.Options;
using AppNutricion.BusinessLogic.Services;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;
using AppNutricion.Tests.Foods;
using AppNutricion.Tests.Infraestructura;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Moq;
using static AppNutricion.Tests.Infraestructura.UsuarioDePrueba;

namespace AppNutricion.Tests.Asistente;

public class AsistenteRegistroYExportTests(ApiFactoryAuth factory) : IClassFixture<ApiFactoryAuth>
{
    // ---- Elección del modelo ----

    [Fact]
    public void ProviderFake_UsaFakeChatModelSinAvisos()
    {
        var logger = new ListLogger<ChatModelFactory>();

        Assert.IsType<FakeChatModel>(ChatModelFactory.Create(new LlmOptions(), logger, () => throw new InvalidOperationException()));
        Assert.Empty(logger.Warnings);
    }

    [Fact]
    public void ProviderRealSinApiKey_AvisaYUsaFake()
    {
        var logger = new ListLogger<ChatModelFactory>();

        Assert.IsType<FakeChatModel>(ChatModelFactory.Create(new LlmOptions { Provider = "OpenAI" }, logger, () => throw new InvalidOperationException()));
        Assert.Contains(logger.Warnings, w => w.Contains("OpenAI") && w.Contains("ApiKey"));
    }

    [Fact]
    public void LlmOptions_LeeLaSeccionLlm_ConTimeoutPorDefecto()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Llm:Provider"] = "OpenAI", ["Llm:ApiKey"] = "clave", ["Llm:Model"] = "m", ["Llm:BaseUrl"] = "https://llm.test", ["Llm:TimeoutSeconds"] = "2.5",
        }).Build();

        var opciones = LlmOptions.From(config);
        var porDefecto = LlmOptions.From(new ConfigurationBuilder().Build());

        Assert.Equal(("OpenAI", "clave", "m", "https://llm.test", 2.5), (opciones.Provider, opciones.ApiKey, opciones.Model, opciones.BaseUrl, opciones.TimeoutSeconds));
        Assert.Equal(("Fake", (string?)null, (string?)null, (string?)null, 10d), (porDefecto.Provider, porDefecto.ApiKey, porDefecto.Model, porDefecto.BaseUrl, porDefecto.TimeoutSeconds));
    }

    [Fact]
    public void LaApiRegistraElAsistenteYArrancaConProviderSinClave()
    {
        using var conProviderReal = factory.WithWebHostBuilder(b => b.UseSetting("Llm:Provider", "OpenAI"));
        using var scope = conProviderReal.Services.CreateScope();

        Assert.IsType<FakeChatModel>(scope.ServiceProvider.GetRequiredService<IChatModel>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IAssistantManager>());
        Assert.IsType<EmptyRoutineSummaryProvider>(scope.ServiceProvider.GetRequiredService<IRoutineSummaryProvider>());
    }

    [Fact]
    public void LaApiConProviderOpenAICompatibleYClave_RegistraElAdaptador()
    {
        using var conClave = factory.WithWebHostBuilder(b => b
            .UseSetting("Llm:Provider", LlmOptions.OpenAiCompatibleProvider).UseSetting("Llm:ApiKey", "clave-de-test")
            .UseSetting("Llm:BaseUrl", "https://llm.test").UseSetting("Llm:Model", "m"));

        Assert.IsType<OpenAiCompatibleChatModel>(conClave.Services.GetRequiredService<IChatModel>());
    }

    // Development carga los user-secrets (con la clave real) y appsettings.Development.json (OpenAICompatible):
    // los tests nunca deben llamar al proveedor real.
    [Fact]
    public void LosTestsEnDevelopment_UsanElFake()
    {
        // Clave falsa: el test no depende de que la máquina tenga Llm:ApiKey en user-secrets.
        using var base_ = new ApiFactoryDesarrollo();
        using var desarrollo = base_.WithWebHostBuilder(b => b.UseSetting("Llm:ApiKey", "clave-de-test"));

        Assert.IsType<FakeChatModel>(desarrollo.Services.GetRequiredService<IChatModel>());
    }

    // ---- J5: el export muestra las sugerencias ----

    [Fact]
    public async Task Export_IncluyeLasSugerenciasDelAsistente()
    {
        var store = new Mock<IExportStore>();
        var cliente = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.Replace(ServiceDescriptor.Scoped(_ => store.Object))));
        var (id, client) = Nuevo(cliente);
        var baseUtc = new DateTime(2026, 10, 22, 15, 0, 0, DateTimeKind.Utc);
        store.Setup(s => s.GetAsync(id)).ReturnsAsync(new UserDataExport(
            new User { Id = id, Email = "ana@mail.com", PasswordHash = "x", ConsentVersion = "v", CreatedAt = baseUtc }, null, [], [], [], [], [], [], [], [],
            [new Conversation { Title = "comí arroz", CreatedAt = baseUtc, UpdatedAt = baseUtc, Messages = [new ChatMessage { Role = ChatRole.Tool, Content = "buscar_alimento", CreatedAt = baseUtc }] }],
            [AssistantControllerTests.Sugerencia(SuggestionStatus.Accepted, 77)]));

        var response = await client.GetAsync("/api/me/export");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await JsonAsync(response);
        var s = Assert.Single(json.GetProperty("assistantSuggestions").EnumerateArray());
        Assert.Equal(("Arroz hervido", 150m, "Lunch", "Accepted", 77),
            (s.GetProperty("foodName").GetString(), s.GetProperty("quantityGrams").GetDecimal(), s.GetProperty("mealType").GetString(),
             s.GetProperty("status").GetString(), s.GetProperty("mealEntryId").GetInt32()));
        Assert.Equal("Tool", json.GetProperty("conversations")[0].GetProperty("messages")[0].GetProperty("role").GetString());
    }
}
