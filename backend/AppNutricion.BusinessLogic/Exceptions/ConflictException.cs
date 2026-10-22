namespace AppNutricion.BusinessLogic.Exceptions;

// Falta un paso previo (por ejemplo, completar el perfil): el controller responde 409 con este mensaje.
public class ConflictException(string message) : Exception(message);
