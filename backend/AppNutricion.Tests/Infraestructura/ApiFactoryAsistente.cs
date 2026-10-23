using AppNutricion.BusinessLogic.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace AppNutricion.Tests.Infraestructura;

// Para AssistantController: esquema "Test" y IAssistantManager mockeado.
public class ApiFactoryAsistente : ApiFactory
{
    public Mock<IAssistantManager> AssistantManager { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(TestAuthHandler.Esquema)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.Esquema, _ => { });
            services.AddSingleton(AssistantManager.Object);
        });
    }

    public HttpClient CrearCliente(int? userId)
    {
        var client = CreateClient();
        if (userId is not null) client.DefaultRequestHeaders.Add(TestAuthHandler.Header, userId.ToString());
        return client;
    }
}

// Con el límite real del chat (20 por minuto por usuario), para probar el 429.
public class ApiFactoryAsistenteRateLimit : ApiFactoryAsistente
{
    protected override string? LimiteDeChat => null;
}
