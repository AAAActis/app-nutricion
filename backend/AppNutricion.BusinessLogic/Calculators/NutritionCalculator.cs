using AppNutricion.BusinessLogic.Constants;
using AppNutricion.Entities.Enums;

namespace AppNutricion.BusinessLogic.Calculators;

public enum BmiCategory { Underweight, Normal, Overweight, ObesityClassI, ObesityClassII, ObesityClassIII }

public record NutritionInput(
    BiologicalSex Sex,
    DateOnly BirthDate,
    decimal HeightCm,
    decimal WeightKg,
    ActivityLevel ActivityLevel,
    GoalType GoalType,
    decimal WeeklyRateKg,          // el pedido; el aplicado puede ser menor (límites de seguridad)
    decimal? TargetWeightKg,
    DateOnly Today);

public record NutritionResult(
    int Calories,
    decimal ProteinG,
    decimal CarbsG,
    decimal FatG,
    decimal Bmi,
    BmiCategory BmiCategory,
    decimal AppliedWeeklyRateKg,
    bool WeeklyRateCapped,
    bool CalorieFloorApplied,
    decimal? ProjectedWeeks,
    DateOnly? ProjectedDate);

// Motor de macros (J8): funciones puras, sin base ni HTTP. Constantes en NutritionDefaults.
public static class NutritionCalculator
{
    public static int AgeAt(DateOnly birthDate, DateOnly on)
    {
        var age = on.Year - birthDate.Year;
        // Se compara mes y día: un nacido el 29/2 cumple el 1/3 en los años no bisiestos.
        if ((on.Month, on.Day).CompareTo((birthDate.Month, birthDate.Day)) < 0) age--;
        return age;
    }

    public static decimal Bmr(BiologicalSex sex, decimal weightKg, decimal heightCm, int age) =>
        NutritionDefaults.BmrWeightFactor * weightKg
        + NutritionDefaults.BmrHeightFactor * heightCm
        - NutritionDefaults.BmrAgeFactor * age
        + (sex == BiologicalSex.Male ? NutritionDefaults.BmrMaleConstant : NutritionDefaults.BmrFemaleConstant);

    public static decimal Tdee(decimal bmr, ActivityLevel activityLevel) => bmr * activityLevel switch
    {
        ActivityLevel.Sedentary => NutritionDefaults.ActivitySedentary,
        ActivityLevel.Light => NutritionDefaults.ActivityLight,
        ActivityLevel.Moderate => NutritionDefaults.ActivityModerate,
        ActivityLevel.Active => NutritionDefaults.ActivityActive,
        ActivityLevel.VeryActive => NutritionDefaults.ActivityVeryActive,
        _ => throw new ArgumentOutOfRangeException(nameof(activityLevel)),
    };

    public static decimal MaxWeeklyRateKg(GoalType goalType, decimal weightKg) => goalType switch
    {
        GoalType.LoseFat => Math.Min(NutritionDefaults.MaxLossKgPerWeek, weightKg * NutritionDefaults.MaxLossFractionOfWeightPerWeek),
        GoalType.GainMuscle => NutritionDefaults.MaxGainKgPerWeek,
        _ => 0m,
    };

    public static decimal Bmi(decimal weightKg, decimal heightCm)
    {
        var heightM = heightCm / 100m;
        return Round1(weightKg / (heightM * heightM));
    }

    public static BmiCategory CategoryFor(decimal bmi) => bmi switch
    {
        < NutritionDefaults.BmiUnderweightBelow => BmiCategory.Underweight,
        < NutritionDefaults.BmiOverweightFrom => BmiCategory.Normal,
        < NutritionDefaults.BmiObesityIFrom => BmiCategory.Overweight,
        < NutritionDefaults.BmiObesityIIFrom => BmiCategory.ObesityClassI,
        < NutritionDefaults.BmiObesityIIIFrom => BmiCategory.ObesityClassII,
        _ => BmiCategory.ObesityClassIII,
    };

    private static decimal Round1(decimal value) => Math.Round(value, 1, MidpointRounding.AwayFromZero);

    public static NutritionResult Calculate(NutritionInput input) =>
        new(0, 0, 0, 0, 0, default, 0, false, false, null, null);
}
