using System.Net;
using System.Text;
using System.Text.Json;
using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.BusinessLogic.Options;
using AppNutricion.BusinessLogic.Services;
using AppNutricion.Entities.Enums;
using AppNutricion.Tests.Foods;
using Microsoft.Extensions.Logging;
using Moq;

namespace AppNutricion.Tests.Asistente;

// Handler falso: guarda el request (y su body) y responde lo que se le diga. Sin red.
public sealed class HandlerFalso(Func<HttpResponseMessage> responder) : HttpMessageHandler
{
    public HttpRequestMessage? Request { get; private set; }
    public string Body { get; private set; } = "";

    public static HandlerFalso Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(() => new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") });

    public IHttpClientFactory Factory()
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(this, disposeHandler: false));
        return factory.Object;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Request = request;
        Body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        return responder();
    }
}

public class OpenAiCompatibleChatModelTests
{
    public static readonly LlmOptions Opciones = new()
    {
        Provider = LlmOptions.OpenAiCompatibleProvider, BaseUrl = "https://llm.test/v1beta/openai", Model = "modelo-x", ApiKey = "clave-de-test",
    };

    private const string RespuestaTexto = """{"choices":[{"index":0,"message":{"role":"assistant","content":"Te faltan 1500 kcal."},"finish_reason":"stop"}]}""";

    private static readonly ChatToolDefinition Buscar = new("buscar_alimento", "Busca alimentos",
        """{"type":"object","properties":{"q":{"type":"string"}},"required":["q"]}""");

    private readonly ListLogger<OpenAiCompatibleChatModel> _logger = new();

    private OpenAiCompatibleChatModel Modelo(HandlerFalso handler) => new(handler.Factory(), Opciones, _logger);

    private static readonly ChatTurn[] Conversacion =
    [
        new(ChatRole.User, "comí arroz"),
        new(ChatRole.Assistant, "", [new ChatToolCall("call-1", "buscar_alimento", """{"q":"arroz"}""", """{"google":{"thought_signature":"firma"}}""")]),
        new(ChatRole.Tool, """{"resultados":[]}""", ToolCallId: "call-1"),
    ];

    [Fact]
    public async Task ElRequest_LlevaBearerModeloToolsYMensajesEnOrden()
    {
        var handler = HandlerFalso.Json(RespuestaTexto);

        await Modelo(handler).CompleteAsync("prompt de sistema", Conversacion, [Buscar], CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("https://llm.test/v1beta/openai/chat/completions", handler.Request.RequestUri!.ToString());
        Assert.Equal(("Bearer", "clave-de-test"), (handler.Request.Headers.Authorization!.Scheme, handler.Request.Headers.Authorization.Parameter));
        var body = JsonDocument.Parse(handler.Body).RootElement;
        Assert.Equal("modelo-x", body.GetProperty("model").GetString());

        var tool = Assert.Single(body.GetProperty("tools").EnumerateArray());
        Assert.Equal("function", tool.GetProperty("type").GetString());
        Assert.Equal("buscar_alimento", tool.GetProperty("function").GetProperty("name").GetString());
        Assert.Equal("Busca alimentos", tool.GetProperty("function").GetProperty("description").GetString());
        Assert.Equal("q", tool.GetProperty("function").GetProperty("parameters").GetProperty("required")[0].GetString());

        var mensajes = body.GetProperty("messages").EnumerateArray().ToArray();
        Assert.Equal(["system", "user", "assistant", "tool"], mensajes.Select(m => m.GetProperty("role").GetString()));
        Assert.Equal("prompt de sistema", mensajes[0].GetProperty("content").GetString());
        Assert.Equal("comí arroz", mensajes[1].GetProperty("content").GetString());
        var llamada = Assert.Single(mensajes[2].GetProperty("tool_calls").EnumerateArray());
        Assert.Equal(("call-1", "function", "buscar_alimento", """{"q":"arroz"}"""), (llamada.GetProperty("id").GetString(), llamada.GetProperty("type").GetString(),
            llamada.GetProperty("function").GetProperty("name").GetString(), llamada.GetProperty("function").GetProperty("arguments").GetString()));
        // Gemini 3 exige devolver la firma de pensamiento de cada tool call tal como vino.
        Assert.Equal("firma", llamada.GetProperty("extra_content").GetProperty("google").GetProperty("thought_signature").GetString());
        Assert.Equal(("call-1", """{"resultados":[]}"""), (mensajes[3].GetProperty("tool_call_id").GetString(), mensajes[3].GetProperty("content").GetString()));
    }

    [Fact]
    public async Task RespuestaConToolCalls_SeMapeaALlamadasConSuFirma()
    {
        var handler = HandlerFalso.Json("""
            {"choices":[{"index":0,"finish_reason":"tool_calls","message":{"role":"assistant","content":null,"tool_calls":[
              {"id":"fc-1","type":"function","function":{"name":"buscar_alimento","arguments":"{\"q\":\"arroz\"}"},"extra_content":{"google":{"thought_signature":"abc"}}},
              {"id":"fc-2","type":"function","function":{"name":"totales_del_dia","arguments":"{}"}}]}}]}
            """);

        var completion = await Modelo(handler).CompleteAsync("p", [new ChatTurn(ChatRole.User, "comí arroz")], [Buscar], CancellationToken.None);

        Assert.Null(completion.Text);
        Assert.Equal(2, completion.ToolCalls.Count);
        Assert.Equal(("fc-1", "buscar_alimento", """{"q":"arroz"}"""), (completion.ToolCalls[0].Id, completion.ToolCalls[0].Name, completion.ToolCalls[0].ArgumentsJson));
        Assert.Equal("abc", JsonDocument.Parse(completion.ToolCalls[0].ProviderMetadataJson!).RootElement.GetProperty("google").GetProperty("thought_signature").GetString());
        Assert.Equal(("fc-2", "totales_del_dia", "{}", (string?)null),
            (completion.ToolCalls[1].Id, completion.ToolCalls[1].Name, completion.ToolCalls[1].ArgumentsJson, completion.ToolCalls[1].ProviderMetadataJson));
    }

    [Fact]
    public async Task RespuestaFinal_SeMapeaATextoSinLlamadas_YSeLogueaElStatusSinElBody()
    {
        var handler = HandlerFalso.Json(RespuestaTexto);

        var completion = await Modelo(handler).CompleteAsync("prompt con los totales 4817 kcal", [new ChatTurn(ChatRole.User, "cuánto me falta")], [Buscar], CancellationToken.None);

        Assert.Equal("Te faltan 1500 kcal.", completion.Text);
        Assert.Empty(completion.ToolCalls);
        var logs = string.Join("\n", _logger.Entries.Select(e => e.Message));
        Assert.Contains("200", logs);
        Assert.DoesNotContain("4817", logs);
        Assert.DoesNotContain("cuánto me falta", logs);
        Assert.DoesNotContain("clave-de-test", logs);
    }

    // El cuerpo trae choices válidos: lo que corta es el status, no el JSON.
    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task StatusDeError_TiraHttpRequestException(HttpStatusCode status)
    {
        var handler = HandlerFalso.Json(RespuestaTexto, status);

        await Assert.ThrowsAsync<HttpRequestException>(() => Modelo(handler).CompleteAsync("p", [new ChatTurn(ChatRole.User, "hola")], [Buscar], CancellationToken.None));
        Assert.Contains(_logger.Entries, e => e.Message.Contains(((int)status).ToString()));
        Assert.DoesNotContain(_logger.Entries, e => e.Message.Contains("clave-de-test"));
    }

    [Theory]
    [InlineData("""{"choices":[{"message":""")]
    [InlineData("""{"choices":[]}""")]
    public async Task JsonInvalidoOSinChoices_TiraJsonException(string json)
    {
        var handler = HandlerFalso.Json(json);

        await Assert.ThrowsAsync<JsonException>(() => Modelo(handler).CompleteAsync("p", [new ChatTurn(ChatRole.User, "hola")], [Buscar], CancellationToken.None));
    }

    // ---- Elección del modelo ----

    [Fact]
    public void ProviderOpenAICompatibleConClave_UsaElAdaptador()
    {
        var logger = new ListLogger<ChatModelFactory>();
        var adaptador = new OpenAiCompatibleChatModel(HandlerFalso.Json(RespuestaTexto).Factory(), Opciones, _logger);

        Assert.Same(adaptador, ChatModelFactory.Create(Opciones, logger, () => adaptador));
        Assert.Empty(logger.Warnings);
    }

    [Theory]
    [InlineData(null, "https://llm.test", "m")]
    [InlineData("clave", null, "m")]
    [InlineData("clave", "https://llm.test", null)]
    public void ProviderOpenAICompatibleSinClaveBaseUrlOModelo_AvisaYUsaFake(string? apiKey, string? baseUrl, string? model)
    {
        var logger = new ListLogger<ChatModelFactory>();
        var opciones = new LlmOptions { Provider = LlmOptions.OpenAiCompatibleProvider, ApiKey = apiKey, BaseUrl = baseUrl, Model = model };

        Assert.IsType<FakeChatModel>(ChatModelFactory.Create(opciones, logger, () => throw new InvalidOperationException("no se debía crear")));
        Assert.Contains(logger.Warnings, w => w.Contains(LlmOptions.OpenAiCompatibleProvider));
    }

    [Fact]
    public void ProviderDesconocidoConClave_AvisaYUsaFake()
    {
        var logger = new ListLogger<ChatModelFactory>();
        var opciones = new LlmOptions { Provider = "Otro", ApiKey = "clave", BaseUrl = "https://llm.test", Model = "m" };

        Assert.IsType<FakeChatModel>(ChatModelFactory.Create(opciones, logger, () => throw new InvalidOperationException("no se debía crear")));
        Assert.Contains(logger.Warnings, w => w.Contains("Otro"));
    }
}
