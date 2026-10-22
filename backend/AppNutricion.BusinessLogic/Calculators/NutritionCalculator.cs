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

    public static NutritionResult Calculate(NutritionInput input)
    {
        var age = AgeAt(input.BirthDate, input.Today);
        if (input.GoalType == GoalType.LoseFat && age < NutritionDefaults.MinAgeForLoseFat)
            throw new ArgumentException($"Menores de {NutritionDefaults.MinAgeForLoseFat} años no pueden elegir bajar de peso.", "goalType");

        var tdee = Tdee(Bmr(input.Sex, input.WeightKg, input.HeightCm, age), input.ActivityLevel);

        var maxRate = MaxWeeklyRateKg(input.GoalType, input.WeightKg);
        var requestedRate = input.GoalType == GoalType.Maintain ? 0m : input.WeeklyRateKg;
        var appliedRate = Math.Min(requestedRate, maxRate);
        var dailyAdjustment = appliedRate * NutritionDefaults.KcalPerKg / NutritionDefaults.DaysPerWeek;
        var rawCalories = input.GoalType == GoalType.LoseFat ? tdee - dailyAdjustment : tdee + dailyAdjustment;

        var floor = input.Sex == BiologicalSex.Male ? NutritionDefaults.CalorieFloorMale : NutritionDefaults.CalorieFloorFemale;
        var floorApplied = rawCalories < floor;
        var calories = (int)Math.Round(floorApplied ? floor : rawCalories, MidpointRounding.AwayFromZero);

        // Macros sobre las kcal ya redondeadas: proteína por kg, grasa como % de las kcal, carbohidratos el resto.
        var proteinPerKg = input.GoalType == GoalType.Maintain
            ? NutritionDefaults.ProteinGPerKgMaintain
            : NutritionDefaults.ProteinGPerKgLoseOrGain;
        var proteinG = proteinPerKg * input.WeightKg;
        var fatKcal = calories * NutritionDefaults.FatFractionOfCalories;
        var fatG = fatKcal / NutritionDefaults.KcalPerGramFat;
        var carbsG = Math.Max(0m, (calories - proteinG * NutritionDefaults.KcalPerGramProtein - fatKcal) / NutritionDefaults.KcalPerGramCarbs);

        var bmi = Bmi(input.WeightKg, input.HeightCm);

        decimal? weeks = null;
        DateOnly? projectedDate = null;
        if (input.TargetWeightKg is { } target && appliedRate > 0)
        {
            var exactWeeks = Math.Abs(input.WeightKg - target) / appliedRate;
            weeks = Round1(exactWeeks);
            projectedDate = input.Today.AddDays((int)Math.Ceiling(exactWeeks * NutritionDefaults.DaysPerWeek));
        }

        return new NutritionResult(
            calories, Round1(proteinG), Round1(carbsG), Round1(fatG), bmi, CategoryFor(bmi),
            appliedRate, requestedRate > maxRate, floorApplied, weeks, projectedDate);
    }
}
