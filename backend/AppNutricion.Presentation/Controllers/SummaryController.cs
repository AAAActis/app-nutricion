using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.Presentation.Auth;
using AppNutricion.Presentation.Output;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppNutricion.Presentation.Controllers;

[ApiController]
[Authorize]
[Route("api/summary")]
public class SummaryController(ISummaryManager summaryManager, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("daily")]
    public async Task<ActionResult<DailySummaryOutput>> Daily([FromQuery] DateOnly? date)
    {
        var userId = currentUser.RequireUserId();
        try
        {
            return DailySummaryOutput.From(await summaryManager.GetDailyAsync(userId, date));
        }
        catch (ArgumentException ex) when (ex.ParamName is not null)
        {
            return this.ValidationProblemFor(ex);
        }
    }

    [HttpGet("weekly")]
    public async Task<ActionResult<WeeklySummaryOutput>> Weekly([FromQuery] DateOnly? endDate)
    {
        var userId = currentUser.RequireUserId();
        try
        {
            return WeeklySummaryOutput.From(await summaryManager.GetWeeklyAsync(userId, endDate));
        }
        catch (ArgumentException ex) when (ex.ParamName is not null)
        {
            return this.ValidationProblemFor(ex);
        }
    }
}
