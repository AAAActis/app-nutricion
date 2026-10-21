using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.Presentation.Output;
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
            return BadRequest(ex.Message);
        }
    }
}
