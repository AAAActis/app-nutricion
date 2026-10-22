using AppNutricion.BusinessLogic.Calculators;
using AppNutricion.Entities.Enums;

namespace AppNutricion.Tests.Macros;

// Casos calculados a mano. Caso base: hombre de 30 años, 180 cm, 80 kg, actividad moderada.
// TMB = 10·80 + 6,25·180 − 5·30 + 5 = 1780. Gasto total = 1780 · 1,55 = 2759.
// Ajuste por objetivo = ritmo · 7700 / 7 = ritmo · 1100 kcal/día.
public class NutritionCalculatorCalculateTests
{
    private static readonly DateOnly Hoy = new(2026, 10, 22);

    private static NutritionInput Base(GoalType objetivo, decimal ritmo, decimal? meta = null) =>
        new(BiologicalSex.Male, new DateOnly(1996, 10, 22), 180m, 80m, ActivityLevel.Moderate, objetivo, ritmo, meta, Hoy);

    // Grasa = 25 % de 2759 = 689,75 kcal → 76,64 g. Proteína = 1,6 · 80 = 128 g (512 kcal).
    // Carbohidratos = (2759 − 512 − 689,75) / 4 = 389,31 g.
    [Fact]
    public void Calculate_Maintain_GastoTotalSinAjusteYProteina16()
    {
        var r = NutritionCalculator.Calculate(Base(GoalType.Maintain, 0m));

        Assert.Equal(2759, r.Calories);
        Assert.Equal(128.0m, r.ProteinG);
        Assert.Equal(76.6m, r.FatG);
        Assert.Equal(389.3m, r.CarbsG);
        Assert.Equal(0m, r.AppliedWeeklyRateKg);
        Assert.False(r.WeeklyRateCapped);
        Assert.False(r.CalorieFloorApplied);
        Assert.Equal(24.7m, r.Bmi);
        Assert.Equal(BmiCategory.Normal, r.BmiCategory);
        Assert.Null(r.ProjectedWeeks);
        Assert.Null(r.ProjectedDate);
    }

    // 2759 − 0,5 · 1100 = 2209. Proteína 1,8 · 80 = 144 g (576 kcal). Grasa 552,25 kcal → 61,36 g.
    // Carbohidratos (2209 − 576 − 552,25) / 4 = 270,19 g. Proyección: 5 kg / 0,5 = 10 semanas = 70 días.
    [Fact]
    public void Calculate_LoseFat_RestaElAjusteYProyecta()
    {
        var r = NutritionCalculator.Calculate(Base(GoalType.LoseFat, 0.5m, 75m));

        Assert.Equal(2209, r.Calories);
        Assert.Equal(144.0m, r.ProteinG);
        Assert.Equal(61.4m, r.FatG);
        Assert.Equal(270.2m, r.CarbsG);
        Assert.Equal(0.5m, r.AppliedWeeklyRateKg);
        Assert.False(r.WeeklyRateCapped);
        Assert.Equal(10.0m, r.ProjectedWeeks);
        Assert.Equal(new DateOnly(2026, 12, 31), r.ProjectedDate);
    }

    // Pide 1 kg/semana, pero el máximo es el 1 % de 80 = 0,8. 2759 − 880 = 1879. 8 kg / 0,8 = 10 semanas.
    [Fact]
    public void Calculate_LoseFatPorEncimaDelMaximo_RecortaElRitmoYAvisa()
    {
        var r = NutritionCalculator.Calculate(Base(GoalType.LoseFat, 1.0m, 72m));

        Assert.Equal(0.8m, r.AppliedWeeklyRateKg);
        Assert.True(r.WeeklyRateCapped);
        Assert.Equal(1879, r.Calories);
        Assert.Equal(10.0m, r.ProjectedWeeks);
    }

    // Justo en el máximo (0,8): no se recorta ni se avisa.
    [Fact]
    public void Calculate_LoseFatJustoEnElMaximo_NoRecorta()
    {
        var r = NutritionCalculator.Calculate(Base(GoalType.LoseFat, 0.8m));

        Assert.Equal(0.8m, r.AppliedWeeklyRateKg);
        Assert.False(r.WeeklyRateCapped);
    }

    // 2759 + 0,25 · 1100 = 3034. Grasa 758,5 kcal → 84,28 g. Carbohidratos (3034 − 576 − 758,5) / 4 = 424,875 g.
    // 5 kg / 0,25 = 20 semanas = 140 días.
    [Fact]
    public void Calculate_GainMuscle_SumaElAjuste()
    {
        var r = NutritionCalculator.Calculate(Base(GoalType.GainMuscle, 0.25m, 85m));

        Assert.Equal(3034, r.Calories);
        Assert.Equal(144.0m, r.ProteinG);
        Assert.Equal(84.3m, r.FatG);
        Assert.Equal(424.9m, r.CarbsG);
        Assert.Equal(20.0m, r.ProjectedWeeks);
        Assert.Equal(Hoy.AddDays(140), r.ProjectedDate);
    }

    // Máximo de ganancia 0,5: pide 0,8 → 0,5. 2759 + 550 = 3309.
    [Fact]
    public void Calculate_GainMusclePorEncimaDelMaximo_RecortaA05()
    {
        var r = NutritionCalculator.Calculate(Base(GoalType.GainMuscle, 0.8m));

        Assert.Equal(0.5m, r.AppliedWeeklyRateKg);
        Assert.True(r.WeeklyRateCapped);
        Assert.Equal(3309, r.Calories);
    }

    // 3 kg / 0,4 = 7,5 semanas = 52,5 días → se redondea hacia arriba a 53 días.
    [Fact]
    public void Calculate_SemanasNoEnteras_FechaRedondeaElDiaHaciaArriba()
    {
        var r = NutritionCalculator.Calculate(Base(GoalType.LoseFat, 0.4m, 77m));

        Assert.Equal(7.5m, r.ProjectedWeeks);
        Assert.Equal(Hoy.AddDays(53), r.ProjectedDate);
    }

    [Fact]
    public void Calculate_SinMeta_SinProyeccion()
    {
        var r = NutritionCalculator.Calculate(Base(GoalType.LoseFat, 0.5m));

        Assert.Null(r.ProjectedWeeks);
        Assert.Null(r.ProjectedDate);
    }

    // Maintain ignora el ritmo pedido: ajuste 0, sin aviso y sin proyección aunque haya meta.
    [Fact]
    public void Calculate_MaintainConRitmoYMeta_IgnoraElRitmoYNoProyecta()
    {
        var r = NutritionCalculator.Calculate(Base(GoalType.Maintain, 0.5m, 75m));

        Assert.Equal(2759, r.Calories);
        Assert.Equal(0m, r.AppliedWeeklyRateKg);
        Assert.False(r.WeeklyRateCapped);
        Assert.Null(r.ProjectedWeeks);
    }

    // Mujer de 25 años, 165 cm, 60 kg, sedentaria: TMB 1345,25 · 1,2 = 1614,3. − 550 = 1064,3 < 1200 → piso.
    // Proteína 1,8 · 60 = 108 g (432 kcal). Grasa 300 kcal → 33,3 g. Carbohidratos (1200 − 432 − 300) / 4 = 117 g.
    [Fact]
    public void Calculate_MujerPorDebajoDe1200_UsaElPisoYAvisa()
    {
        var input = new NutritionInput(BiologicalSex.Female, new DateOnly(2001, 10, 22), 165m, 60m,
            ActivityLevel.Sedentary, GoalType.LoseFat, 0.5m, null, Hoy);

        var r = NutritionCalculator.Calculate(input);

        Assert.Equal(1200, r.Calories);
        Assert.True(r.CalorieFloorApplied);
        Assert.Equal(108.0m, r.ProteinG);
        Assert.Equal(33.3m, r.FatG);
        Assert.Equal(117.0m, r.CarbsG);
    }

    // Hombre de 30 años, 160 cm, 60 kg, sedentario: TMB 1455 · 1,2 = 1746. Ritmo 0,6 (máximo) → − 660 = 1086 < 1500.
    [Fact]
    public void Calculate_HombrePorDebajoDe1500_UsaElPisoYAvisa()
    {
        var input = new NutritionInput(BiologicalSex.Male, new DateOnly(1996, 10, 22), 160m, 60m,
            ActivityLevel.Sedentary, GoalType.LoseFat, 0.6m, null, Hoy);

        var r = NutritionCalculator.Calculate(input);

        Assert.Equal(1500, r.Calories);
        Assert.True(r.CalorieFloorApplied);
    }

    // Mujer de 61 años, 156 cm, 49,1 kg, sedentaria: TMB = 491 + 975 − 305 − 161 = 1000 → 1200 justo. No es "debajo".
    [Fact]
    public void Calculate_JustoEnElPiso_NoAvisa()
    {
        var input = new NutritionInput(BiologicalSex.Female, new DateOnly(1965, 10, 22), 156m, 49.1m,
            ActivityLevel.Sedentary, GoalType.Maintain, 0m, null, Hoy);

        var r = NutritionCalculator.Calculate(input);

        Assert.Equal(1200, r.Calories);
        Assert.False(r.CalorieFloorApplied);
    }

    // Mujer de 100 años, 100 cm, 130 kg: piso 1200. Proteína 1,8 · 130 = 234 g (936 kcal) + grasa 300 kcal > 1200.
    // Los carbohidratos darían −9 g: nunca negativos.
    [Fact]
    public void Calculate_ProteinaYGrasaSuperanLasKcal_CarbohidratosEnCero()
    {
        var input = new NutritionInput(BiologicalSex.Female, new DateOnly(1926, 10, 22), 100m, 130m,
            ActivityLevel.Sedentary, GoalType.LoseFat, 1.0m, null, Hoy);

        var r = NutritionCalculator.Calculate(input);

        Assert.Equal(1200, r.Calories);
        Assert.Equal(234.0m, r.ProteinG);
        Assert.Equal(0m, r.CarbsG);
    }

    // Mujer de 25 años, sedentaria, Maintain: 1614,3 → 1614.
    [Fact]
    public void Calculate_RedondeaLasKcalAEntero()
    {
        var input = new NutritionInput(BiologicalSex.Female, new DateOnly(2001, 10, 22), 165m, 60m,
            ActivityLevel.Sedentary, GoalType.Maintain, 0m, null, Hoy);

        Assert.Equal(1614, NutritionCalculator.Calculate(input).Calories);
    }

    // Recalculo de GET /targets: el ritmo ya es el aplicado y no se recorta de nuevo.
    // 1 kg pedido con 80 kg (máximo 0,8): 2759 − 1100 = 1659 kcal; 5 kg / 1 = 5 semanas.
    [Fact]
    public void Calculate_SinLimitesDeRitmo_UsaElRitmoTalCualSinAvisar()
    {
        var input = Base(GoalType.LoseFat, 1.0m, 75m) with { ApplyRateLimits = false };

        var r = NutritionCalculator.Calculate(input);

        Assert.Equal(1.0m, r.AppliedWeeklyRateKg);
        Assert.False(r.WeeklyRateCapped);
        Assert.Equal(1659, r.Calories);
        Assert.Equal(5.0m, r.ProjectedWeeks);
    }

    // Menores de 18: LoseFat no se permite.
    [Fact]
    public void Calculate_MenorDe18ConLoseFat_LanzaArgumentExceptionDeGoalType()
    {
        var input = Base(GoalType.LoseFat, 0.5m) with { BirthDate = new DateOnly(2008, 10, 23) };   // 17 años

        var ex = Assert.Throws<ArgumentException>(() => NutritionCalculator.Calculate(input));

        Assert.Equal("goalType", ex.ParamName);
    }

    [Theory]
    [InlineData("2008-10-22", GoalType.LoseFat)]     // cumple 18 hoy
    [InlineData("2008-10-23", GoalType.Maintain)]    // 17
    [InlineData("2008-10-23", GoalType.GainMuscle)]  // 17
    public void Calculate_18OMenorSinLoseFat_Calcula(string nacimiento, GoalType objetivo)
    {
        var input = Base(objetivo, 0.25m) with { BirthDate = DateOnly.Parse(nacimiento) };

        var r = NutritionCalculator.Calculate(input);

        Assert.True(r.Calories > 0);
    }
}
