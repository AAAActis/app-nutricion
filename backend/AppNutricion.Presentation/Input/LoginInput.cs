namespace AppNutricion.Presentation.Input;

public record LoginInput(string? Email, string? Password)
{
    // MVC, en Trace, loguea los argumentos de la acción con ToString(): la contraseña nunca va.
    public override string ToString() => $"LoginInput {{ Email = {Email}, Password = {SecretText.Mask} }}";
}
