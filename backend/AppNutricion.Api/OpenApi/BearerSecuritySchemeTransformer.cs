using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace AppNutricion.Api.OpenApi;

// Declara el esquema "Bearer" (JWT) en el documento OpenAPI para que Swagger UI muestre
// el botón "Authorize" y mande el header "Authorization: Bearer <token>" en cada request.
public sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public const string NombreEsquema = "Bearer";

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[NombreEsquema] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Pegá solo el token (sin la palabra Bearer)."
        };

        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(NombreEsquema, document)] = []
        });

        return Task.CompletedTask;
    }
}
