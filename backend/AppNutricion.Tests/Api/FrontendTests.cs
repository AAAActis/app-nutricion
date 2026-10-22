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

    // El texto de consentimiento del registro es el de la versión que guarda la API.
    [Fact]
    public void Consentimiento_LaVersionDelFrontendEsLaDeLaApi()
    {
        var consent = Archivos(".js").SingleOrDefault(a => a.Ruta == "js/consent.js");

        Assert.NotNull(consent.Contenido);
        Assert.Contains($"CONSENT_VERSION = \"{ConsentVersions.Current}\"", consent.Contenido);
    }
}
