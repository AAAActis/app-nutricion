using System.Globalization;
using Microsoft.AspNetCore.Http;

namespace AppNutricion.Presentation.Auth;

public class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public int? UserId
    {
        get
        {
            // JwtBearer con MapInboundClaims = false: el claim se llama "sub", igual que en el token.
            var sub = httpContextAccessor.HttpContext?.User.FindFirst("sub")?.Value;
            return int.TryParse(sub, CultureInfo.InvariantCulture, out var id) ? id : null;
        }
    }

    public int RequireUserId() =>
        UserId ?? throw new InvalidOperationException("No hay userId en el token: ¿falta [Authorize] en el endpoint?");
}
