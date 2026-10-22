namespace AppNutricion.BusinessLogic.Constants;

// Valores por defecto del motor de macros (J8). Son ESTIMACIONES, no consejo médico:
// todos están en "Decisiones a ratificar" del PR de J7/J8 y se cambian solo acá.
public static class NutritionDefaults
{
    // Mifflin-St Jeor (Mifflin MD, St Jeor ST y col., Am J Clin Nutr 1990;51:241-7).
    public const decimal BmrWeightFactor = 10m;
    public const decimal BmrHeightFactor = 6.25m;
    public const decimal BmrAgeFactor = 5m;
    public const decimal BmrMaleConstant = 5m;
    public const decimal BmrFemaleConstant = -161m;

    // Factores de actividad.
    public const decimal ActivitySedentary = 1.2m;
    public const decimal ActivityLight = 1.375m;
    public const decimal ActivityModerate = 1.55m;
    public const decimal ActivityActive = 1.725m;
    public const decimal ActivityVeryActive = 1.9m;

    // 1 kg de peso corporal ≈ 7700 kcal.
    public const decimal KcalPerKg = 7700m;
    public const decimal DaysPerWeek = 7m;

    // Límites de seguridad del ritmo semanal.
    public const decimal MaxLossKgPerWeek = 1m;
    public const decimal MaxLossFractionOfWeightPerWeek = 0.01m;   // 1 % del peso
    public const decimal MaxGainKgPerWeek = 0.5m;

    // Piso calórico: si el cálculo queda abajo, se usa el piso y se avisa.
    public const decimal CalorieFloorMale = 1500m;
    public const decimal CalorieFloorFemale = 1200m;

    // Menores de esta edad no pueden elegir LoseFat.
    public const int MinAgeForLoseFat = 18;

    // Macros.
    public const decimal ProteinGPerKgLoseOrGain = 1.8m;
    public const decimal ProteinGPerKgMaintain = 1.6m;
    public const decimal ProteinMaxFractionOfCalories = 0.30m;   // tope: con pesos altos, g/kg superaba las kcal
    public const decimal FatFractionOfCalories = 0.25m;
    public const decimal KcalPerGramProtein = 4m;
    public const decimal KcalPerGramCarbs = 4m;
    public const decimal KcalPerGramFat = 9m;

    // IMC, cortes de la OMS.
    public const decimal BmiUnderweightBelow = 18.5m;
    public const decimal BmiOverweightFrom = 25m;
    public const decimal BmiObesityIFrom = 30m;
    public const decimal BmiObesityIIFrom = 35m;
    public const decimal BmiObesityIIIFrom = 40m;

    public const string Disclaimer = "Estimación, no es consejo médico.";
}
