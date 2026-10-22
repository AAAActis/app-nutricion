using System.Security.Claims;
using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.Presentation.Input;
using AppNutricion.Presentation.Output;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AppNutricion.Presentation.Controllers;

[ApiController]
[Authorize]
[Route("api/meals")]
public class MealsController(IMealEntryManager mealManager) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<MealEntryOutput>> Create(CreateMealEntryInput input)
    {
        if (CurrentUserId() is not { } userId) return Unauthorized();
        try
        {
            var entry = await mealManager.CreateAsync(userId, input.FoodId, input.Quantity, input.IsUnits, input.MealType, input.Date, input.RecipeId);
            return entry is null ? NotFound() : Created((string?)null, MealEntryOutput.From(entry));
        }
        catch (ArgumentException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: ex.Message);
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<MealEntryOutput>> Update(int id, UpdateMealEntryInput input)
    {
        if (CurrentUserId() is not { } userId) return Unauthorized();
        try
        {
            var entry = await mealManager.UpdateAsync(id, userId, input.Quantity, input.IsUnits, input.MealType, input.Date);
            return entry is null ? NotFound() : MealEntryOutput.From(entry);
        }
        catch (ArgumentException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: ex.Message);
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (CurrentUserId() is not { } userId) return Unauthorized();
        return await mealManager.DeleteAsync(id, userId) ? NoContent() : NotFound();
    }

    [HttpGet]
    public async Task<ActionResult<List<MealEntryOutput>>> List([FromQuery] DateOnly? date)
    {
        if (CurrentUserId() is not { } userId) return Unauthorized();
        try
        {
            var entries = await mealManager.ListAsync(userId, date);
            return entries.Select(MealEntryOutput.From).ToList();
        }
        catch (ArgumentException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: ex.Message);
        }
    }

    [HttpPost("copy")]
    public async Task<ActionResult<List<MealEntryOutput>>> Copy(CopyMealsInput input)
    {
        if (CurrentUserId() is not { } userId) return Unauthorized();
        try
        {
            var copies = await mealManager.CopyAsync(userId, input.FromDate, input.ToDate, input.MealType);
            return copies.Select(MealEntryOutput.From).ToList();
        }
        catch (ArgumentException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: ex.Message);
        }
    }

    // El userId sale solo del token (D6): nunca del body ni de la query.
    private int? CurrentUserId() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
