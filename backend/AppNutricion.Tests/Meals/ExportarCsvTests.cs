using System.Globalization;
using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.BusinessLogic.Managers;
using AppNutricion.BusinessLogic.Services;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;
using Moq;

namespace AppNutricion.Tests.Meals;

public class ExportarCsvTests
{
    private const int UserId = 7;
    private const string Cabecera = "fecha,tipo,alimento,gramos,kcal,proteinas_g,carbohidratos_g,grasas_g\r\n";
    private static readonly DateOnly Desde = new(2026, 10, 19);
    private static readonly DateOnly Hasta = new(2026, 10, 20);

    private readonly Mock<IMealEntryStore> _store = new();
    private readonly MealEntryManager _manager;

    public ExportarCsvTests()
    {
        _store.Setup(s => s.GetRangeAsync(UserId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>())).ReturnsAsync([]);
        _manager = new MealEntryManager(
            _store.Object, Mock.Of<IFoodManager>(), new UserToday(Mock.Of<IProfileStore>(), TimeProvider.System), TimeProvider.System);
    }

    private void ConComidas(params MealEntry[] entradas) =>
        _store.Setup(s => s.GetRangeAsync(UserId, Desde, Hasta)).ReturnsAsync(entradas.ToList());

    private static MealEntry Entrada(string nombre, MealType tipo = MealType.Lunch, DateOnly? dia = null) => new()
    {
        UserId = UserId, ItemName = nombre, Date = dia ?? Hasta, MealType = tipo,
        QuantityGrams = 150.5m, SavedCalories = 375.25m, SavedProtein = 15, SavedCarbs = 45.1m, SavedFat = 7.5m,
    };

    private Task<string> Exportar() => _manager.ExportCsvAsync(UserId, Desde, Hasta);

    // La última línea del CSV (sin el salto de línea final).
    private async Task<string> FilaDe(string nombre)
    {
        ConComidas(Entrada(nombre));
        return (await Exportar())[Cabecera.Length..^2];
    }

    [Fact]
    public async Task SinComidasEnElRango_SoloLaCabecera() => Assert.Equal(Cabecera, await Exportar());

    [Fact]
    public async Task Filas_EnElOrdenDelStoreConPuntoDecimalAunqueLaCulturaUseComa()
    {
        var cultura = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("es-AR");
        try
        {
            ConComidas(Entrada("Pan", MealType.Breakfast, Desde), Entrada("Arroz"));

            Assert.Equal(
                Cabecera +
                "2026-10-19,Breakfast,Pan,150.5,375.25,15,45.1,7.5\r\n" +
                "2026-10-20,Lunch,Arroz,150.5,375.25,15,45.1,7.5\r\n",
                await Exportar());
        }
        finally
        {
            CultureInfo.CurrentCulture = cultura;
        }
    }

    [Theory]
    [InlineData("Pan, casero", "\"Pan, casero\"")]
    [InlineData("Pan \"Bimbo\"", "\"Pan \"\"Bimbo\"\"\"")]
    [InlineData("Pan\ncasero", "\"Pan\ncasero\"")]
    public async Task Alimento_EscapadoRfc4180(string nombre, string esperado) =>
        Assert.StartsWith($"2026-10-20,Lunch,{esperado},", await FilaDe(nombre));

    [Theory]
    [InlineData("=1+2", "'=1+2")]
    [InlineData("+54 9", "'+54 9")]
    [InlineData("-5 kg", "'-5 kg")]
    [InlineData("@SUMA(A1)", "'@SUMA(A1)")]
    [InlineData("\tPan", "'\tPan")]
    [InlineData("=HYPERLINK(\"http://x\",\"y\")", "\"'=HYPERLINK(\"\"http://x\"\",\"\"y\"\")\"")]
    public async Task Alimento_QueEmpiezaComoFormula_QuedaNeutralizado(string nombre, string esperado) =>
        Assert.StartsWith($"2026-10-20,Lunch,{esperado},", await FilaDe(nombre));

    [Fact]
    public async Task UsaElUserIdYElRango()
    {
        await Exportar();

        _store.Verify(s => s.GetRangeAsync(UserId, Desde, Hasta), Times.Once);
    }

    public static TheoryData<DateOnly?, DateOnly?, string> RangosInvalidos => new()
    {
        { null, Hasta, "from" },
        { Desde, null, "to" },
        { Hasta, Desde, "to" },                              // from > to
        { new(2025, 10, 20), new(2026, 10, 20), "to" },      // 367 días
    };

    [Theory]
    [MemberData(nameof(RangosInvalidos))]
    public async Task RangoInvalido_ArgumentExceptionConElCampo(DateOnly? desde, DateOnly? hasta, string campo)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _manager.ExportCsvAsync(UserId, desde, hasta));

        Assert.Equal(campo, ex.ParamName);
        _store.Verify(s => s.GetRangeAsync(It.IsAny<int>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>()), Times.Never);
    }

    [Fact]
    public async Task Rango_DeUnDiaYDe366Dias_Valido()
    {
        await _manager.ExportCsvAsync(UserId, Hasta, Hasta);
        await _manager.ExportCsvAsync(UserId, new DateOnly(2025, 10, 21), Hasta);

        _store.Verify(s => s.GetRangeAsync(UserId, It.IsAny<DateOnly>(), Hasta), Times.Exactly(2));
    }
}
