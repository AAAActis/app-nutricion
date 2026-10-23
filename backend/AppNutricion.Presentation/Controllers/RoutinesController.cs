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
public class RoutinesController(IRoutineManager routineManager, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("api/exercises")]
    public async Task<ActionResult<List<ExerciseOutput>>> ListExercises([FromQuery] string? muscleGroup)
    {
        _ = currentUser.RequireUserId();   // catálogo compartido: no filtra, pero como todo [Authorize] exige un "sub" válido
        try
        {
            return (await routineManager.ListExercisesAsync(muscleGroup)).Select(ExerciseOutput.From).ToList();
        }
        catch (ArgumentException ex) when (ex.ParamName is not null)
        {
            return this.ValidationProblemFor(ex);
        }
    }

    [HttpGet("api/routines")]
    public async Task<ActionResult<List<RoutineSummaryOutput>>> List()
    {
        var userId = currentUser.RequireUserId();
        return (await routineManager.ListAsync(userId)).Select(RoutineSummaryOutput.From).ToList();
    }

    [HttpGet("api/routines/{id:int}")]
    public async Task<ActionResult<RoutineOutput>> Get(int id)
    {
        var userId = currentUser.RequireUserId();
        var routine = await routineManager.GetAsync(id, userId);
        return routine is null ? NotFound() : RoutineOutput.From(routine);
    }

    [HttpPost("api/routines")]
    public async Task<ActionResult<RoutineOutput>> Create(CreateRoutineInput input)
    {
        var userId = currentUser.RequireUserId();
        try
        {
            var routine = await routineManager.CreateAsync(userId, new RoutineDraft(input.Name, input.Description, input.Days));
            return Created($"/api/routines/{routine.Id}", RoutineOutput.From(routine));
        }
        catch (ArgumentException ex) when (ex.ParamName is not null)
        {
            return this.ValidationProblemFor(ex);
        }
    }

    [HttpDelete("api/routines/{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = currentUser.RequireUserId();
        try
        {
            return await routineManager.DeleteAsync(id, userId) ? NoContent() : NotFound();
        }
        catch (ConflictException ex)
        {
            return this.ConflictProblemFor(ex);
        }
    }
}
