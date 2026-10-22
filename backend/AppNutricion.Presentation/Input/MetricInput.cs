namespace AppNutricion.Presentation.Input;

// Date null = hoy en la zona horaria del perfil.
public record MetricInput(decimal? WeightKg, DateOnly? Date);
