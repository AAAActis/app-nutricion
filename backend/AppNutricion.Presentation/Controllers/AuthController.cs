using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.Presentation.Input;
using AppNutricion.Presentation.Output;
using AppNutricion.Presentation.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AppNutricion.Presentation.Controllers;

[ApiController]
[Route("api/auth")]
// Login y registro: 5 intentos por minuto y por IP (compartidos entre los dos) → 429 con Retry-After.
[EnableRateLimiting(RateLimitPolicies.Auth)]
public class AuthController(IAuthManager authManager) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<UserOutput>> Register(RegisterInput input)
    {
        RegisterResult result;
        try
        {
            result = await authManager.RegisterAsync(input.Email, input.Password, input.ConsentAccepted);
        }
        catch (ArgumentException ex) when (ex.ParamName is not null)
        {
            return this.ValidationProblemFor(ex);
        }

        if (result.Status == RegisterStatus.EmailAlreadyRegistered)
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "El email ya está registrado.");
        return Created("/api/me", UserOutput.From(result.User!));
    }

    // Mismo 401 si el email no existe o la contraseña no coincide: no se revela qué emails están registrados.
    [HttpPost("login")]
    public async Task<ActionResult<LoginOutput>> Login(LoginInput input)
    {
        var result = await authManager.LoginAsync(input.Email, input.Password);
        if (result is null)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Email o contraseña incorrectos.");
        return LoginOutput.From(result);
    }
}
