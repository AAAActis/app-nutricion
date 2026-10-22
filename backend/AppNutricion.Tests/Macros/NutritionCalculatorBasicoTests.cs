using AppNutricion.BusinessLogic.Calculators;
using AppNutricion.Entities.Enums;

namespace AppNutricion.Tests.Macros;

// Casos calculados a mano.
public class NutritionCalculatorBasicoTests
{
    [Theory]
    [InlineData("2000-10-22", "2026-10-22", 26)]   // el día del cumpleaños ya los cumplió
    [InlineData("2000-10-22", "2026-10-21", 25)]   // el día anterior todavía no
    [InlineData("2000-10-22", "2000-10-22", 0)]
    [InlineData("2004-02-29", "2025-02-28", 20)]   // nacido un 29/2: en año no bisiesto cumple el 1/3
    [InlineData("2004-02-29", "2025-03-01", 21)]
    [InlineData("2004-02-29", "2028-02-29", 24)]
    public void AgeAt_CuentaAniosCumplidos(string nacimiento, string fecha, int edad)
    {
        Assert.Equal(edad, NutritionCalculator.AgeAt(DateOnly.Parse(nacimiento), DateOnly.Parse(fecha)));
    }

    // Hombre: 10·80 + 6,25·180 − 5·30 + 5 = 800 + 1125 − 150 + 5 = 1780.
    // Mujer: 10·60 + 6,25·165 − 5·25 − 161 = 600 + 1031,25 − 125 − 161 = 1345,25.
    [Theory]
    [InlineData(BiologicalSex.Male, 80, 180, 30, 1780)]
    [InlineData(BiologicalSex.Female, 60, 165, 25, 1345.25)]
    public void Bmr_MifflinStJeor(BiologicalSex sexo, decimal kg, decimal cm, int edad, decimal esperado)
    {
        Assert.Equal(esperado, NutritionCalculator.Bmr(sexo, kg, cm, edad));
    }

    [Theory]
    [InlineData(ActivityLevel.Sedentary, 1200)]
    [InlineData(ActivityLevel.Light, 1375)]
    [InlineData(ActivityLevel.Moderate, 1550)]
    [InlineData(ActivityLevel.Active, 1725)]
    [InlineData(ActivityLevel.VeryActive, 1900)]
    public void Tdee_MultiplicaLaTmbPorElFactorDeActividad(ActivityLevel nivel, decimal esperado)
    {
        Assert.Equal(esperado, NutritionCalculator.Tdee(1000m, nivel));
    }

    // Un nivel fuera del enum es un bug de quien llama (el manager valida antes).
    [Fact]
    public void Tdee_NivelNoDefinido_LanzaArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NutritionCalculator.Tdee(1000m, (ActivityLevel)99));
    }
}
