using System.Text;
using AppNutricion.Api.OpenApi;
using AppNutricion.BusinessLogic.Options;
using AppNutricion.BusinessLogic.Registrations;
using AppNutricion.DataAccess.Registrations;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Falta ConnectionStrings:DefaultConnection en user-secrets.");
// Sin una clave de al menos 32 bytes no se arranca. El mensaje nunca incluye la clave.
var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
var jwtOptions = jwtSection.Get<JwtOptions>() ?? new JwtOptions();
if (Encoding.UTF8.GetByteCount(jwtOptions.Key) < JwtOptions.MinKeyBytes)
    throw new InvalidOperationException($"Falta Jwt:Key en user-secrets o tiene menos de {JwtOptions.MinKeyBytes} bytes.");
builder.Services.Configure<JwtOptions>(jwtSection);

builder.Services.AddDataAccess(connectionString);
builder.Services.AddBusinessLogic();
builder.Services.AddControllers();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());
builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();

// CORS: por defecto (lista vacía) solo mismo origen. Para habilitar otro frontend se agregan
// sus orígenes en Cors:AllowedOrigins del appsettings del entorno o por variable de entorno.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// Primero del pipeline: cualquier excepción no manejada sale como ProblemDetails 500 genérico
// (se loguea completa, pero al cliente no le llega ni el mensaje ni el stack trace).
app.UseExceptionHandler();
// Los errores sin cuerpo (404, 405...) también salen como ProblemDetails: un solo formato de error.
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "AppNutricion v1"));
}

app.UseHttpsRedirection();
app.UseCors();

app.MapHealthChecks("/health");

app.MapControllers();
app.Run();
