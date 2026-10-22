using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.Presentation.Auth;
using AppNutricion.Presentation.Input;
using AppNutricion.Presentation.Output;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AppNutricion.Presentation.Controllers;

[ApiController]
[Authorize]
[Route("api/me")]
public class MeController(IAuthManager authManager, IAccountManager accountManager, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<UserOutput>> Get()
    {
        var userId = currentUser.RequireUserId();
        var user = await authManager.GetUserAsync(userId);
        // Token válido de una cuenta que ya no existe: no hay sesión.
        if (user is null)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "La cuenta del token ya no existe.");
        return UserOutput.From(user);
    }

    // Todos los datos del usuario (Ley 25.326, derecho de acceso). Sin hash ni refresh tokens.
    [HttpGet("export")]
    public async Task<ActionResult<ExportOutput>> Export()
    {
        var data = await accountManager.ExportAsync(currentUser.RequireUserId());
        if (data is null)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "La cuenta del token ya no existe.");
        return ExportOutput.From(data);
    }

    // Borra la cuenta con todos sus datos. Exige la contraseña actual.
    [HttpDelete]
    public async Task<IActionResult> Delete(DeleteAccountInput input)
    {
        if (!await accountManager.DeleteAsync(currentUser.RequireUserId(), input.Password))
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "La contraseña no coincide.");
        return NoContent();
    }
}
