using Microsoft.AspNetCore.Http;

namespace AppNutricion.Presentation.Auth;

public class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public int? UserId => null;

    public int RequireUserId() => 0;
}
