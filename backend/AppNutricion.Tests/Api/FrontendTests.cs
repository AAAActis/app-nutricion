using System.Net;
using System.Text.RegularExpressions;
using AppNutricion.BusinessLogic.Constants;
using AppNutricion.Tests.Infraestructura;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

namespace AppNutricion.Tests.Api;

// Frontend base (J12): HTML/CSS/JS sin frameworks en wwwroot, mismo origen que la API.
// El JS no se prueba con xUnit (ver checklist manual en el PR): acá se prueba que se sirve, que no rompe la API
// y reglas de seguridad que se pueden revisar sobre los archivos.
public class FrontendTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private IFileProvider WebRoot => factory.Services.GetRequiredService<IWebHostEnvironment>().WebRootFileProvider;

    // Todos los archivos de wwwroot con esa extensión (recorre subcarpetas).
    private List<(string Ruta, string Contenido)> Archivos(string extension, string carpeta = "")
    {
        var encontrados = new List<(string, string)>();
        foreach (var item in WebRoot.GetDirectoryContents(carpeta))
        {
            var ruta = carpeta.Length == 0 ? item.Name : $"{carpeta}/{item.Name}";
            if (item.IsDirectory) encontrados.AddRange(Archivos(extension, ruta));
            else if (item.Name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                using var reader = new StreamReader(item.CreateReadStream());
                encontrados.Add((ruta, reader.ReadToEnd()));
            }
        }
        return encontrados;
    }

    [Fact]
    public async Task Raiz_200ConElIndexHtml()
    {
        var response = await factory.CreateClient().GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("<script type=\"module\" src=\"js/app.js\"></script>", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/js/api.js", "text/javascript")]
    [InlineData("/js/router.js", "text/javascript")]
    [InlineData("/js/auth.js", "text/javascript")]
    [InlineData("/css/app.css", "text/css")]
    public async Task ArchivosBase_200(string ruta, string tipo)
    {
        var response = await factory.CreateClient().GetAsync(ruta);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(tipo, response.Content.Headers.ContentType?.MediaType);
    }

    // El HTML sale con una CSP que solo permite recursos del mismo origen: sin scripts externos ni inline.
    [Fact]
    public async Task Raiz_ConContentSecurityPolicySoloMismoOrigen()
    {
        var response = await factory.CreateClient().GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var csp = Assert.Single(response.Headers.GetValues("Content-Security-Policy"));
        Assert.Contains("default-src 'self'", csp);
        Assert.Contains("script-src 'self'", csp);
        Assert.DoesNotContain("unsafe-inline", csp);
    }

    [Fact]
    public async Task ApiInexistente_SigueSiendoProblemDetails404()
    {
        var response = await factory.CreateClient().GetAsync("/api/inexistente");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Health_SigueOk()
    {
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateClient().GetAsync("/health")).StatusCode);
    }

    // Seguridad: los datos se muestran con textContent. Ningún JS arma HTML desde texto.
    [Fact]
    public void Js_NuncaUsaInnerHtmlNiParecidos()
    {
        var js = Archivos(".js");

        Assert.NotEmpty(js);
        foreach (var (ruta, contenido) in js)
            foreach (var prohibido in new[] { "innerHTML", "outerHTML", "insertAdjacentHTML", "document.write", "eval(", "new Function" })
                Assert.False(contenido.Contains(prohibido), $"{ruta} usa {prohibido}");
    }

    // Sin CDN ni scripts externos: la demo anda sin internet.
    [Fact]
    public void Html_SinScriptsNiEstilosExternos()
    {
        var html = Archivos(".html");

        Assert.NotEmpty(html);
        foreach (var (ruta, contenido) in html)
        {
            Assert.DoesNotMatch(new Regex(@"<(script|link)[^>]+(src|href)\s*=\s*""(https?:)?//", RegexOptions.IgnoreCase), contenido);
            Assert.False(contenido.Contains("<script>", StringComparison.OrdinalIgnoreCase), $"{ruta} tiene un script inline");
        }
        foreach (var (ruta, contenido) in Archivos(".js"))
            Assert.False(Regex.IsMatch(contenido, @"import\s[^;]*from\s+[""']https?:"), $"{ruta} importa un módulo externo");
    }

    // Leyenda del art. 3 de la Resolución 14/2018 de la AAIP, copiada textual de
    // https://www.argentina.gob.ar/normativa/nacional/resoluci%C3%B3n-14-2018-307621/texto (22/10/2026).
    private const string LeyendaAaip =
        "LA AGENCIA DE ACCESO A LA INFORMACIÓN PÚBLICA, en su carácter de Órgano de Control de la Ley N° 25.326, " +
        "tiene la atribución de atender las denuncias y reclamos que interpongan quienes resulten afectados en sus " +
        "derechos por incumplimiento de las normas vigentes en materia de protección de datos personales";

    // Lo que tiene que decir el consentimiento (Ley 25.326, art. 6, y Res. AAIP 14/2018, art. 3).
    [Theory]
    [InlineData(LeyendaAaip)]
    [InlineData("guardada como hash, que no se puede revertir")]
    [InlineData("Responsable")]
    [InlineData("Nutrición Inteligente")]
    [InlineData("UNC")]
    [InlineData("Finalidad")]
    [InlineData("necesarios para usar el servicio")]
    [InlineData("acceso")]
    [InlineData("rectificación (desde Perfil)")]
    [InlineData("supresión (desde Cuenta)")]
    [InlineData("art. 6 de la Ley 25.326")]
    [InlineData("Contacto: infobbasolutions@gmail.com.")]
    [InlineData("Google Gemini")]
    [InlineData("mensajes que escribís en el chat")]
    [InlineData("nunca tu email ni tu nombre")]
    [InlineData("pueden usarse para mejorar sus servicios")]
    public void Consentimiento_IncluyeLoQuePideLaLey(string texto)
    {
        var consent = Archivos(".js").SingleOrDefault(a => a.Ruta == "js/consent.js");

        Assert.NotNull(consent.Contenido);
        Assert.Contains(texto, consent.Contenido);
    }

    [Fact]
    public void Consentimiento_NoDiceCifrada()
    {
        var consent = Archivos(".js").Single(a => a.Ruta == "js/consent.js");

        Assert.DoesNotContain("cifrada", consent.Contenido);
    }

    // Los datos sí se comparten con Gemini: decir que no se comparten contradice el aviso del asistente.
    [Fact]
    public void Consentimiento_NoDiceQueNoSeComparten()
    {
        var consent = Archivos(".js").Single(a => a.Ruta == "js/consent.js");

        Assert.DoesNotContain("no los compartimos", consent.Contenido);
        Assert.Contains("Son datos de salud: no los vendemos. Solo los compartimos con el servicio de inteligencia artificial del asistente (Google Gemini), como se explica más abajo.", consent.Contenido);
    }

    // El texto que acepta el usuario no puede tener datos sin completar.
    [Fact]
    public void Consentimiento_SinNadaPorCompletar()
    {
        var consent = Archivos(".js").Single(a => a.Ruta == "js/consent.js");

        Assert.DoesNotContain("<COMPLETAR", consent.Contenido);
    }

    // El aviso del proveedor de IA va antes de los derechos, como el resto de lo que se hace con los datos.
    [Fact]
    public void Consentimiento_AvisoDeGeminiAntesDeLosDerechos()
    {
        var consent = Archivos(".js").Single(a => a.Ruta == "js/consent.js").Contenido;

        var gemini = consent.IndexOf("Google Gemini", StringComparison.Ordinal);
        var derechos = consent.IndexOf("Tus derechos", StringComparison.Ordinal);
        Assert.True(gemini >= 0 && gemini < derechos, $"Google Gemini en {gemini}, Tus derechos en {derechos}");
    }

    // El texto de consentimiento del registro es el de la versión que guarda la API.
    [Fact]
    public void Consentimiento_LaVersionDelFrontendEsLaDeLaApi()
    {
        var consent = Archivos(".js").SingleOrDefault(a => a.Ruta == "js/consent.js");

        Assert.NotNull(consent.Contenido);
        Assert.Contains($"CONSENT_VERSION = \"{ConsentVersions.Current}\"", consent.Contenido);
    }
}
