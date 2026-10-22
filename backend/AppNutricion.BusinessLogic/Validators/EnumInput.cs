namespace AppNutricion.BusinessLogic.Validators;

public static class EnumInput
{
    // Solo nombres del enum, sin distinguir mayúsculas. Un número ("1") no vale, aunque Enum.TryParse lo acepte.
    public static bool TryParse<TEnum>(string? text, out TEnum value) where TEnum : struct, Enum
    {
        value = default;
        var name = Enum.GetNames<TEnum>().FirstOrDefault(n => string.Equals(n, text, StringComparison.OrdinalIgnoreCase));
        return name is not null && Enum.TryParse(name, out value);
    }
}
