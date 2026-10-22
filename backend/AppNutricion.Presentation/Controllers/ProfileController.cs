using AppNutricion.BusinessLogic.Exceptions;
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
[Route("api/me/profile")]
public class ProfileController(IProfileManager profileManager, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ProfileOutput>> Get()
    {
        var profile = await profileManager.GetAsync(currentUser.RequireUserId());
        if (profile is null)
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Todavía no completaste el perfil.");
        return ProfileOutput.From(profile);
    }

    // Crea o actualiza (1 a 1 con el usuario). 200 en los dos casos.
    [HttpPut]
    public async Task<ActionResult<ProfileOutput>> Put(ProfileInput input)
    {
        try
        {
            var data = new ProfileData(input.BirthDate, input.BiologicalSex, input.HeightCm, input.ActivityLevel, input.TimeZoneId);
            var result = await profileManager.SaveAsync(currentUser.RequireUserId(), data);
            return ProfileOutput.From(result.Profile);
        }
        catch (ConflictException ex)
        {
            return this.ConflictProblemFor(ex);
        }
        catch (ArgumentException ex) when (ex.ParamName is not null)
        {
            return this.ValidationProblemFor(ex);
        }
    }
}
