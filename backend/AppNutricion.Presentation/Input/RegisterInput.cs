namespace AppNutricion.Presentation.Input;

// Campos opcionales a propósito: las validaciones son del AuthManager, que devuelve el campo que falla.
public record RegisterInput(string? Email, string? Password, bool ConsentAccepted)
{
    // MVC, en Trace, loguea los argumentos de la acción con ToString(): la contraseña nunca va.
    public override string ToString() => $"RegisterInput {{ Email = {Email}, Password = {SecretText.Mask}, ConsentAccepted = {ConsentAccepted} }}";
}
