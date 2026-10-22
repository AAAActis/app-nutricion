namespace AppNutricion.BusinessLogic.Constants;

// Límites de validación de perfil, peso y objetivos (J7). Valores por defecto: están en
// "Decisiones a ratificar" del PR de J7/J8 y se cambian solo acá.
public static class HealthDataLimits
{
    public const int MinAgeYears = 14;
    public const int MaxAgeYears = 100;
    public const decimal MinHeightCm = 100m;
    public const decimal MaxHeightCm = 250m;
    public const decimal MinWeightKg = 20m;
    public const decimal MaxWeightKg = 400m;
    public const int MaxMetricAgeYears = 2;            // no se cargan pesos de hace más de 2 años
    public const int TrendWindowRecords = 7;           // tendencia: últimos 7 registros contra los 7 anteriores
    public const int DecimalsStored = 2;               // numeric(9,2) en la base
}
