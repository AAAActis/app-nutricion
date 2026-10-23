using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.Presentation.Auth;
using AppNutricion.Presentation.Input;
using AppNutricion.Presentation.Output;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppNutricion.Presentation.Controllers;

// Recetas (L9): solo las del usuario del token; la de otro → 404.
[ApiController]
[Authorize]
[Route("api/recipes")]
public class RecipesController(IRecipeManager recipeManager, ICurrentUser currentUser) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<RecipeOutput>> Create(RecipeInput input)
    {
        var userId = currentUser.RequireUserId();
        try
        {
            var recipe = await recipeManager.CreateAsync(userId, ToData(input));
            return Created((string?)null, RecipeOutput.From(recipe));
        }
        catch (ArgumentException ex) when (ex.ParamName is not null)
        {
            return this.ValidationProblemFor(ex);
        }
    }

    [HttpGet]
    public async Task<ActionResult<List<RecipeOutput>>> List()
    {
        var recipes = await recipeManager.ListAsync(currentUser.RequireUserId());
        return recipes.Select(RecipeOutput.From).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<RecipeOutput>> Get(int id)
    {
        var recipe = await recipeManager.GetAsync(id, currentUser.RequireUserId());
        return recipe is null ? NotFound() : RecipeOutput.From(recipe);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<RecipeOutput>> Update(int id, RecipeInput input)
    {
        var userId = currentUser.RequireUserId();
        try
        {
            var recipe = await recipeManager.UpdateAsync(id, userId, ToData(input));
            return recipe is null ? NotFound() : RecipeOutput.From(recipe);
        }
        catch (ArgumentException ex) when (ex.ParamName is not null)
        {
            return this.ValidationProblemFor(ex);
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = currentUser.RequireUserId();
        return await recipeManager.DeleteAsync(id, userId) ? NoContent() : NotFound();
    }

    // Un ingrediente null en el JSON llega como 0 g: 400 con su campo, no un 500.
    private static RecipeData ToData(RecipeInput i) =>
        new(i.Name, i.Servings, i.Ingredients?.Select(x => new RecipeIngredientData(x?.FoodId ?? 0, x?.QuantityGrams ?? 0)).ToList());
}
