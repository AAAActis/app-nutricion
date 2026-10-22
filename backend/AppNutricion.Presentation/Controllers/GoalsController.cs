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
[Route("api/me")]
public class GoalsController(IGoalManager goalManager, ICurrentUser currentUser) : ControllerBase
{
    // Crea el objetivo nuevo (desactiva el anterior) y devuelve sus targets.
    [HttpPost("goals")]
    public async Task<ActionResult<TargetsOutput>> Create(GoalInput input)
    {
        try
        {
            var targets = await goalManager.CreateAsync(currentUser.RequireUserId(), input.GoalType, input.WeeklyRateKg, input.TargetWeightKg);
            return Created("/api/me/targets", TargetsOutput.From(targets));
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

    // Historial, del más nuevo al más viejo.
    [HttpGet("goals")]
    public async Task<ActionResult<List<GoalOutput>>> List()
    {
        var goals = await goalManager.ListAsync(currentUser.RequireUserId());
        return goals.Select(GoalOutput.From).ToList();
    }

    [HttpGet("targets")]
    public async Task<ActionResult<TargetsOutput>> Targets()
    {
        var targets = await goalManager.GetTargetsAsync(currentUser.RequireUserId());
        if (targets is null)
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Todavía no tenés un objetivo activo.");
        return TargetsOutput.From(targets);
    }
}
