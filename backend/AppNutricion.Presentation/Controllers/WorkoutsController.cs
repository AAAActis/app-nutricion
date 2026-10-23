using AppNutricion.BusinessLogic.Exceptions;
using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.Presentation.Auth;
using AppNutricion.Presentation.Input;
using AppNutricion.Presentation.Output;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppNutricion.Presentation.Controllers;

[ApiController]
[Authorize]
[Route("api/workouts")]
public class WorkoutsController(IWorkoutManager workoutManager, ICurrentUser currentUser) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<WorkoutOutput>> Create(CreateWorkoutInput input)
    {
        var userId = currentUser.RequireUserId();
        try
        {
            var workout = await workoutManager.CreateAsync(userId,
                new WorkoutDraft(input.RoutineDayId, input.Date, input.DurationMinutes, input.Notes, input.Sets));
            return workout is null ? NotFound() : Created($"/api/workouts/{workout.Id}", WorkoutOutput.From(workout));
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

    [HttpGet]
    public async Task<ActionResult<List<WorkoutSummaryOutput>>> List([FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        var userId = currentUser.RequireUserId();
        try
        {
            return (await workoutManager.ListAsync(userId, from, to)).Select(WorkoutSummaryOutput.From).ToList();
        }
        catch (ArgumentException ex) when (ex.ParamName is not null)
        {
            return this.ValidationProblemFor(ex);
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<WorkoutOutput>> Get(int id)
    {
        var userId = currentUser.RequireUserId();
        var workout = await workoutManager.GetAsync(id, userId);
        return workout is null ? NotFound() : WorkoutOutput.From(workout);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = currentUser.RequireUserId();
        return await workoutManager.DeleteAsync(id, userId) ? NoContent() : NotFound();
    }
}
