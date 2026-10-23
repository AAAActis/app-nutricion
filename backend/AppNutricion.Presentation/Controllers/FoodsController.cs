using AppNutricion.BusinessLogic.Exceptions;
using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.Presentation.Auth;
using AppNutricion.Presentation.Input;
using AppNutricion.Presentation.Output;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppNutricion.Presentation.Controllers;

[ApiController]
[Route("api/foods")]
public class FoodsController(IFoodManager foodManager, ICurrentUser currentUser) : ControllerBase
{
    // Búsqueda, detalle y código de barras son públicos: con token suman los alimentos propios del usuario.
    [HttpGet("search")]
    public async Task<ActionResult<List<FoodOutput>>> Search([FromQuery] string? q, [FromQuery] int? limit)
    {
        var foods = await foodManager.SearchAsync(q, currentUser.UserId, limit);
        return foods.Select(FoodOutput.From).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<FoodOutput>> GetById(int id)
    {
        var food = await foodManager.GetByIdAsync(id, currentUser.UserId);
        return food is null ? NotFound() : FoodOutput.From(food);
    }

    [HttpGet("barcode/{code}")]
    public async Task<ActionResult<FoodOutput>> GetByBarcode(string code)
    {
        try
        {
            var food = await foodManager.GetByBarcodeAsync(code, currentUser.UserId);
            return food is null ? NotFound() : FoodOutput.From(food);
        }
        catch (ArgumentException ex) when (ex.ParamName is not null)
        {
            return this.ValidationProblemFor(ex);
        }
    }

    [Authorize]
    [HttpPost("{id:int}/favorite")]
    public async Task<IActionResult> AddFavorite(int id)
    {
        var userId = currentUser.RequireUserId();
        return await foodManager.AddFavoriteAsync(id, userId) ? NoContent() : NotFound();
    }

    [Authorize]
    [HttpDelete("{id:int}/favorite")]
    public async Task<IActionResult> RemoveFavorite(int id)
    {
        var userId = currentUser.RequireUserId();
        return await foodManager.RemoveFavoriteAsync(id, userId) ? NoContent() : NotFound();
    }

    [Authorize]
    [HttpGet("favorites")]
    public async Task<ActionResult<List<FoodOutput>>> GetFavorites()
    {
        var userId = currentUser.RequireUserId();
        var foods = await foodManager.GetFavoritesAsync(userId);
        return foods.Select(FoodOutput.From).ToList();
    }

    [Authorize]
    [HttpGet("recent")]
    public async Task<ActionResult<List<FoodOutput>>> GetRecent([FromQuery] int? limit)
    {
        var userId = currentUser.RequireUserId();
        var foods = await foodManager.GetRecentAsync(userId, limit);
        return foods.Select(FoodOutput.From).ToList();
    }

    // Personalizados (L8): solo los Custom del usuario del token; de otro o compartido → 404.
    [Authorize]
    [HttpPost("custom")]
    public async Task<ActionResult<FoodOutput>> CreateCustom(CustomFoodInput input)
    {
        var userId = currentUser.RequireUserId();
        try
        {
            var food = await foodManager.CreateCustomAsync(userId, ToData(input));
            return Created((string?)null, FoodOutput.From(food));
        }
        catch (ArgumentException ex) when (ex.ParamName is not null)
        {
            return this.ValidationProblemFor(ex);
        }
    }

    [Authorize]
    [HttpGet("custom")]
    public async Task<ActionResult<List<FoodOutput>>> GetCustom()
    {
        var foods = await foodManager.GetCustomAsync(currentUser.RequireUserId());
        return foods.Select(FoodOutput.From).ToList();
    }

    [Authorize]
    [HttpGet("custom/{id:int}")]
    public async Task<ActionResult<FoodOutput>> GetCustomById(int id)
    {
        var food = await foodManager.GetCustomByIdAsync(id, currentUser.RequireUserId());
        return food is null ? NotFound() : FoodOutput.From(food);
    }

    [Authorize]
    [HttpPut("custom/{id:int}")]
    public async Task<ActionResult<FoodOutput>> UpdateCustom(int id, CustomFoodInput input)
    {
        var userId = currentUser.RequireUserId();
        try
        {
            var food = await foodManager.UpdateCustomAsync(id, userId, ToData(input));
            return food is null ? NotFound() : FoodOutput.From(food);
        }
        catch (ArgumentException ex) when (ex.ParamName is not null)
        {
            return this.ValidationProblemFor(ex);
        }
    }

    [Authorize]
    [HttpDelete("custom/{id:int}")]
    public async Task<IActionResult> DeleteCustom(int id)
    {
        var userId = currentUser.RequireUserId();
        try
        {
            return await foodManager.DeleteCustomAsync(id, userId) ? NoContent() : NotFound();
        }
        catch (ConflictException ex)
        {
            return this.ConflictProblemFor(ex);
        }
    }

    private static CustomFoodData ToData(CustomFoodInput i) =>
        new(i.Name, i.Brand, i.CaloriesPer100g, i.ProteinPer100g, i.CarbsPer100g, i.FatPer100g, i.PortionName, i.PortionWeightGrams);
}
