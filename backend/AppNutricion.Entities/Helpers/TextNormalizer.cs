using System.Globalization;
using System.Text;

namespace AppNutricion.Entities.Helpers;

public static class TextNormalizer
{
    // FormD separa la letra de su tilde (á → a + ´, ñ → n + ˜); después se descartan las marcas.
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var sb = new StringBuilder(text.Length);
        foreach (var c in text.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return string.Join(' ', sb.ToString().ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
