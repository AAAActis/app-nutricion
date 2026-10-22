using AppNutricion.BusinessLogic.Calculators;
using AppNutricion.Entities.Enums;

namespace AppNutricion.Tests.Macros;

public class NutritionCalculatorImcYRitmoTests
{
    // 80 / 1,8² = 24,691… → 24,7.   60 / 1,65² = 22,038… → 22,0.   45 / 1,7² = 15,570… → 15,6.
    [Theory]
    [InlineData(80, 180, 24.7)]
    [InlineData(60, 165, 22.0)]
    [InlineData(45, 170, 15.6)]
    public void Bmi_KgSobreMetrosAlCuadrado_RedondeadoAUnDecimal(decimal kg, decimal cm, decimal esperado)
    {
        Assert.Equal(esperado, NutritionCalculator.Bmi(kg, cm));
    }

    // Cortes de la OMS, con cada borde de los dos lados.
    [Theory]
    [InlineData(18.4, BmiCategory.Underweight)]
    [InlineData(18.5, BmiCategory.Normal)]
    [InlineData(24.9, BmiCategory.Normal)]
    [InlineData(25.0, BmiCategory.Overweight)]
    [InlineData(29.9, BmiCategory.Overweight)]
    [InlineData(30.0, BmiCategory.ObesityClassI)]
    [InlineData(34.9, BmiCategory.ObesityClassI)]
    [InlineData(35.0, BmiCategory.ObesityClassII)]
    [InlineData(39.9, BmiCategory.ObesityClassII)]
    [InlineData(40.0, BmiCategory.ObesityClassIII)]
    public void CategoryFor_CortesDeLaOms(decimal imc, BmiCategory esperada)
    {
        Assert.Equal(esperada, NutritionCalculator.CategoryFor(imc));
    }

    // Pérdida: el menor entre 1 kg/semana y el 1 % del peso. Ganancia: 0,5 kg/semana. Mantener: 0.
    [Theory]
    [InlineData(GoalType.LoseFat, 80, 0.8)]
    [InlineData(GoalType.LoseFat, 100, 1.0)]
    [InlineData(GoalType.LoseFat, 120, 1.0)]
    [InlineData(GoalType.GainMuscle, 80, 0.5)]
    [InlineData(GoalType.Maintain, 80, 0)]
    public void MaxWeeklyRateKg_LimitesDeSeguridad(GoalType objetivo, decimal kg, decimal esperado)
    {
        Assert.Equal(esperado, NutritionCalculator.MaxWeeklyRateKg(objetivo, kg));
    }
}
