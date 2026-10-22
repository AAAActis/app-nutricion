using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.Presentation.Auth;
using AppNutricion.Presentation.Output;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
        catch (ArgumentException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: ex.Message);
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
}
