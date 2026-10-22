using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.Presentation.Output;
using Microsoft.AspNetCore.Authorization;
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
        return UserOutput.From(user!);
    }
}
