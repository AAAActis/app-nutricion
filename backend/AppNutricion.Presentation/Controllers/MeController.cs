using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.Presentation.Output;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AppNutricion.Presentation.Controllers;

[ApiController]
[Authorize]
[Route("api/me")]
public class MeController(IAuthManager authManager) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<UserOutput>> Get()
    {
        // JwtBearer con MapInboundClaims = false: el userId queda en "sub" tal cual lo emitió el token.
        var userId = int.Parse(User.FindFirst("sub")!.Value);
        var user = await authManager.GetUserAsync(userId);
        // Token válido de una cuenta que ya no existe: no hay sesión.
        if (user is null)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "La cuenta del token ya no existe.");
        return UserOutput.From(user);
    }
}
