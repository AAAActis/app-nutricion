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
[Route("api/me/metrics")]
public class MetricsController(IBodyMetricManager metricManager, ICurrentUser currentUser) : ControllerBase
{
    // 201 si es el primer peso de ese día; 200 si corrige el de un día que ya tenía.
    [HttpPost]
    public async Task<ActionResult<MetricOutput>> Post(MetricInput input)
    {
        try
        {
            var result = await metricManager.RecordAsync(currentUser.RequireUserId(), input.WeightKg, input.Date);
            var output = MetricOutput.From(result.Metric);
            return result.Created ? Created("/api/me/metrics", output) : Ok(output);
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
    public async Task<ActionResult<MetricsHistoryOutput>> Get([FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        try
        {
            var history = await metricManager.GetHistoryAsync(currentUser.RequireUserId(), from, to);
            return MetricsHistoryOutput.From(history);
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
}
