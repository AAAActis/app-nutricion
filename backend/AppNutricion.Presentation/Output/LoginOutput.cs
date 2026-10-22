using AppNutricion.BusinessLogic.Interfaces;

namespace AppNutricion.Presentation.Output;

// ExpiresAt en UTC: se serializa en ISO 8601 con "Z".
public record LoginOutput(string AccessToken, DateTime ExpiresAt)
{
    public static LoginOutput From(LoginResult r) => new(r.AccessToken, r.ExpiresAt);
}
