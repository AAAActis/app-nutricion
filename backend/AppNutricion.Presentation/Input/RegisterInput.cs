namespace AppNutricion.Presentation.Input;

// Campos opcionales a propósito: las validaciones son del AuthManager, que devuelve el campo que falla.
public record RegisterInput(string? Email, string? Password, bool ConsentAccepted);
