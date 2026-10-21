namespace AppNutricion.BusinessLogic.Options;

// Sección "Jwt" de la configuración. Key va en user-secrets (o variable de entorno), nunca en el repo.
public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Key { get; set; } = "";
    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";
    public int ExpirationMinutes { get; set; }
}
