namespace AppNutricion.Presentation.Input;

public record DeleteAccountInput(string? Password)
{
    // MVC, en Trace, loguea los argumentos de la acción con ToString(): la contraseña nunca va.
    public override string ToString() => "DeleteAccountInput { Password = *** }";
}
