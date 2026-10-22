using System.Text;
using AppNutricion.Tests.Infraestructura;
using Microsoft.AspNetCore.Hosting;

namespace AppNutricion.Tests.Api;

// HMAC-SHA256 necesita una clave de al menos 32 bytes: si Jwt:Key falta o es corta, la API no arranca
// (mejor fallar al levantar que emitir tokens débiles o romper en el primer login).
public class JwtKeyArranqueTests
{
    private sealed class ApiFactoryConClave(string clave) : ApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("Jwt:Key", clave);
        }
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("   ", 3)]
    [InlineData("clave-de-31-bytes-xxxxxxxxxxxxx", 31)]
    [InlineData("ñññññññññññññññ", 30)]   // 15 caracteres: se cuentan bytes, no caracteres
    public void Arranque_ConJwtKeyFaltanteOCorta_Falla(string clave, int bytes)
    {
        Assert.Equal(bytes, Encoding.UTF8.GetByteCount(clave));
        using var factory = new ApiFactoryConClave(clave);

        var ex = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(ex);
        var mensaje = ex.GetBaseException().Message;
        Assert.Contains("Jwt:Key", mensaje);
        if (!string.IsNullOrWhiteSpace(clave))
            Assert.DoesNotContain(clave, mensaje);   // la clave nunca va al mensaje (ni a los logs)
    }

    [Theory]
    [InlineData("clave-de-32-bytes-xxxxxxxxxxxxxx", 32)]
    [InlineData("ññññññññññññññññ", 32)]   // 16 caracteres
    public void Arranque_ConJwtKeyDe32BytesOMas_Arranca(string clave, int bytes)
    {
        Assert.Equal(bytes, Encoding.UTF8.GetByteCount(clave));
        using var factory = new ApiFactoryConClave(clave);

        var ex = Record.Exception(() => factory.CreateClient());

        Assert.Null(ex);
    }
}
