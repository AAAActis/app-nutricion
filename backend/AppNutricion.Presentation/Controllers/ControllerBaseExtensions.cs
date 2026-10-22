using Microsoft.AspNetCore.Mvc;

namespace AppNutricion.Presentation.Controllers;

public static class ControllerBaseExtensions
{
    // ArgumentException del manager → 400 ValidationProblemDetails con el campo (ParamName) que falla.
    // El mensaje de ArgumentException termina en " (Parameter 'x')": al cliente le llega solo el texto.
    public static ActionResult ValidationProblemFor(this ControllerBase controller, ArgumentException ex)
    {
        controller.ModelState.AddModelError(ex.ParamName!, ex.Message.Replace($" (Parameter '{ex.ParamName}')", ""));
        return controller.ValidationProblem();
    }
}
