using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using AppNutricion.Presentation.RateLimiting;
using System.Net;
using System.Text;

namespace AppNutricion.Tests.Infraestructura;

// API completa con los límites por defecto (no los sube como ApiFactory) y un endpoint de prueba con la
// política del chat, que todavía no se aplica a ningún endpoint real.
public class ApiFactoryRateLimit : ApiFactoryAuth
{
    protected override string? LimiteDeAuth => null;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
            services.AddControllers().AddApplicationPart(typeof(ChatDePruebaController).Assembly));
    }

    // Manda el request con la IP de origen que se pide (TestServer no tiene una por defecto).
    public Task<HttpContext> EnviarAsync(string metodo, string ruta, string ip, string? json = null, string? token = null) =>
        Server.SendAsync(context =>
        {
            context.Request.Method = metodo;
            context.Request.Path = ruta;
            context.Connection.RemoteIpAddress = IPAddress.Parse(ip);
            if (token is not null) context.Request.Headers.Authorization = $"Bearer {token}";
            if (json is not null)
            {
                context.Request.ContentType = "application/json";
                context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
            }
        });
}

// Igual, pero en Development: toma el límite de appsettings.Development.json.
public class ApiFactoryRateLimitDesarrollo : ApiFactoryRateLimit
{
    protected override string Entorno => "Development";
}

[ApiController]
[Route("test/chat")]
public class ChatDePruebaController : ControllerBase
{
    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.AssistantChat)]
    public IActionResult Post() => Ok();
}
