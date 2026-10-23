namespace AppNutricion.BusinessLogic.Constants;

// Versión del texto de consentimiento (Ley 25.326) que acepta el usuario al registrarse.
// Si el texto cambia, se cambia esta fecha y queda registrado qué versión aceptó cada uno.
public static class ConsentVersions
{
    // 2026-10-22: responsable, finalidad, derechos (art. 6 Ley 25.326) y leyenda del art. 3 de la Res. AAIP 14/2018.
    // 2026-10-23: email de contacto y aviso de que el asistente usa Google Gemini (qué datos le llegan y cuáles no).
    public const string Current = "2026-10-23";
}
