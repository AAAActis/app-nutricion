using System.Security.Claims;
using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.Presentation.Output;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AppNutricion.Presentation.Controllers;

[ApiController]
[Route("api/foods")]
public class FoodsController(IFoodManager foodManager) : ControllerBase
{
    // TODO(J2): sumar userId del token para incluir alimentos personalizados.
    private static readonly int? UserId = null;

    [HttpGet("search")]
    public async Task<ActionResult<List<FoodOutput>>> Search([FromQuery] string? q, [FromQuery] int? limit)
    {
        var foods = await foodManager.SearchAsync(q, UserId, limit);
        return foods.Select(FoodOutput.From).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<FoodOutput>> GetById(int id)
    {
        var food = await foodManager.GetByIdAsync(id, UserId);
        return food is null ? NotFound() : FoodOutput.From(food);
    }

    [HttpGet("barcode/{code}")]
    public async Task<ActionResult<FoodOutput>> GetByBarcode(string code)
    {
        try
        {
            var food = await foodManager.GetByBarcodeAsync(code, UserId);
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
        if (CurrentUserId() is not { } userId) return Unauthorized();
        return await foodManager.AddFavoriteAsync(id, userId) ? NoContent() : NotFound();
    }

    [Authorize]
    [HttpDelete("{id:int}/favorite")]
    public async Task<IActionResult> RemoveFavorite(int id)
    {
        if (CurrentUserId() is not { } userId) return Unauthorized();
        return await foodManager.RemoveFavoriteAsync(id, userId) ? NoContent() : NotFound();
    }

    [Authorize]
    [HttpGet("favorites")]
    public async Task<ActionResult<List<FoodOutput>>> GetFavorites()
    {
        if (CurrentUserId() is not { } userId) return Unauthorized();
        var foods = await foodManager.GetFavoritesAsync(userId);
        return foods.Select(FoodOutput.From).ToList();
    }

    [Authorize]
    [HttpGet("recent")]
    public async Task<ActionResult<List<FoodOutput>>> GetRecent([FromQuery] int? limit)
    {
        if (CurrentUserId() is not { } userId) return Unauthorized();
        var foods = await foodManager.GetRecentAsync(userId, limit);
        return foods.Select(FoodOutput.From).ToList();
    }

    // El userId sale solo del token (D6): nunca del body ni de la query.
    private int? CurrentUserId() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
