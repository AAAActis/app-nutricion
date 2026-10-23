namespace AppNutricion.Presentation.Input;

// Opcional a propósito: la validación (1 a 1000 caracteres) es del AssistantManager, que devuelve el campo "content".
public record SendMessageInput(string? Content);
