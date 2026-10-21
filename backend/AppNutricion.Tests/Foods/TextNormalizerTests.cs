using AppNutricion.Entities.Helpers;

namespace AppNutricion.Tests.Foods;

public class TextNormalizerTests
{
    [Theory]
    [InlineData("Café con LECHE", "cafe con leche")]
    [InlineData("  Ñoquis  ", "noquis")]
    [InlineData("pan   de\tmolde", "pan de molde")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData(null, "")]
    public void Normalize_DevuelveMinusculasSinTildesNiEspaciosDeMas(string? input, string expected)
    {
        Assert.Equal(expected, TextNormalizer.Normalize(input));
    }
}
