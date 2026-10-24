using System.Net;
using System.Text.RegularExpressions;
using AppNutricion.BusinessLogic.Constants;
using AppNutricion.Tests.Infraestructura;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
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
    [InlineData("/js/download.js", "text/javascript")]
    [InlineData("/js/screens/rutinas.js", "text/javascript")]
    [InlineData("/js/screens/chat.js", "text/javascript")]
    [InlineData("/js/screens/dia.js", "text/javascript")]
    [InlineData("/js/screens/alimentos.js", "text/javascript")]
    [InlineData("/js/screens/recetas.js", "text/javascript")]
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

    // node.append(null) escribe "null" en la pantalla. Fuera de dom.js se usan append() y setChildren(), que
    // ignoran null/undefined/false; replaceChildren() nativo solo vacío (para limpiar).
    [Fact]
    public void Js_SinAppendNiReplaceChildrenNativosConArgumentos()
    {
        var js = Archivos(".js").Where(a => a.Ruta != "js/dom.js").ToList();

        Assert.NotEmpty(js);
        foreach (var (ruta, contenido) in js)
        {
            Assert.False(Regex.IsMatch(contenido, @"\.append\("), $"{ruta} usa .append( nativo: usá append() o setChildren() de dom.js");
            Assert.False(Regex.IsMatch(contenido, @"\.replaceChildren\(\s*[^)\s]"), $"{ruta} usa .replaceChildren(...) con argumentos: usá setChildren() de dom.js");
        }
    }

    // Reglas como .field { display: grid } le ganan al atributo hidden del navegador: "Ritmo semanal" seguía a la
    // vista con Mantener (B3 del recorrido).
    [Fact]
    public void Css_HiddenLeGanaAlDisplay()
    {
        var css = Archivos(".css").Single(a => a.Ruta == "css/app.css").Contenido;

        // Una sola vez: el #30 y el #31 traían la misma regla.
        Assert.Single(Regex.Matches(css, @"\[hidden\]\s*\{\s*display:\s*none\s*!important;?\s*\}"));
    }

    // B2: la tarjeta se dibuja en Mi día, copia con POST y el 409 queda como aviso (no se relanza al formulario).
    [Fact]
    public void MiDia_CopiarDelDiaAnterior()
    {
        var dia = Archivos(".js").Single(a => a.Ruta == "js/screens/dia.js").Contenido;

        Assert.Matches(new Regex(@"append\(container, \[[^\]]*\bcopyCard\(state, reload, status\),", RegexOptions.Singleline), dia);
        Assert.Contains("api(\"/api/meals/copy\", { method: \"POST\", body })", dia);
        Assert.Matches(new Regex(@"if \(error\.status === 409\) \{\s*setChildren\(status, notice\(""warning"", error\.title\)\);\s*return;\s*\}"), dia);
    }

    // "1 entrenamientos" (B4 del recorrido): un contador seguido de una palabra en plural fijo. Se usa plural() de format.js.
    [Fact]
    public void Js_ContadoresSinPluralFijo()
    {
        var js = Archivos(".js");

        Assert.NotEmpty(js);
        foreach (var (ruta, contenido) in js)
        {
            var match = Regex.Match(contenido, @"\$\{[^}]*(\.length|Count|weeks|streak|servings)[^}]*\}\s+[a-záéíóúñ]+s\b");
            Assert.False(match.Success, $"{ruta} escribe \"{match.Value}\" con el plural fijo: usá plural() de format.js");
            // La misma trampa armada con +: num(workout.setCount, 0) + " series".
            var concatenado = Regex.Match(contenido, @"(\.length|Count|weeks|streak|servings)\b[^""\n;]*\+\s*""\s*[a-záéíóúñ]+s\b");
            Assert.False(concatenado.Success, $"{ruta} escribe \"{concatenado.Value}\" con el plural fijo: usá plural() de format.js");
        }
    }

    // Recetas es de Lauti: el pulido visual del #30 no puede cambiar cómo funciona la pantalla de develop.
    [Theory]
    [InlineData("search.addEventListener(\"input\", () => {")]
    [InlineData("timer = setTimeout(() => runSearch()")]
    [InlineData("if (id !== lastQuery) return;")]
    [InlineData("const MAX_INGREDIENTS = 30;")]
    [InlineData("addButton.disabled = rows.length >= MAX_INGREDIENTS;")]
    [InlineData("name: \"servings\", min: 0.5, max: 20, step: \"0.5\"")]
    [InlineData("name: String(data.get(\"name\") ?? \"\").trim() || null,")]
    [InlineData("if (event.key === \"Enter\") event.preventDefault();")]
    [InlineData("method: recipe ? \"PUT\" : \"POST\",")]
    [InlineData("await api(`/api/recipes/${recipe.id}`, { method: \"DELETE\" });")]
    [InlineData("body: { recipeId: recipe.id, servings: servings === \"\" ? 0 : Number(servings), mealType: data.get(\"mealType\"), date: date || null },")]
    public void Recetas_ConservaElComportamientoDeDevelop(string texto)
    {
        var recetas = Archivos(".js").Single(a => a.Ruta == "js/screens/recetas.js").Contenido;

        Assert.Contains(texto, recetas);
    }

    // El editor arranca abierto con una receta nueva (develop), y "Nueva receta" lo vuelve a abrir vacío.
    [Fact]
    public void Recetas_EditorDeRecetaNuevaAlEntrar()
    {
        var recetas = Archivos(".js").Single(a => a.Ruta == "js/screens/recetas.js").Contenido;

        Assert.Matches(new Regex(@"\]\);\s*edit\(null\);\s*await reload\(\);"), recetas);
        Assert.Matches(new Regex(@"onclick: \(\) => \{\s*edit\(null\);\s*editor\.scrollIntoView\("), recetas);
        Assert.DoesNotMatch(new Regex(@"recipe-editor"", hidden: true"), recetas);
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

    // Toda ruta "/api/…" escrita entre comillas en el JS existe en la API (sin mirar el método). Los ${…} de los
    // template strings cuentan como un valor de ruta ({id:int}) y el query string no se compara.
    [Fact]
    public void Js_SoloLlamaRutasQueExistenEnLaApi()
    {
        _ = factory.Server;   // arranca la app para que los endpoints estén mapeados
        var patrones = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Select(e => e.RoutePattern.RawText?.TrimStart('/'))
            .Where(p => p is not null && p.StartsWith("api/"))
            .Select(p => new Regex("^/" + Regex.Replace(Regex.Escape(p!), @"\\\{[^/]*}", "[^/]+") + "$"))
            .ToList();

        Assert.NotEmpty(patrones);
        foreach (var (ruta, contenido) in Archivos(".js"))
            foreach (Match match in Regex.Matches(contenido, @"[""'`](/api/[^""'`\s?]*)"))
            {
                var path = Regex.Replace(match.Groups[1].Value, @"\$\{[^}]*}", "1");
                Assert.True(patrones.Any(p => p.IsMatch(path)), $"{ruta} llama a {match.Groups[1].Value}, que no existe en la API");
            }
    }

    // Rutinas y chat ya tienen API en develop: no pueden seguir en "Próximamente".
    [Theory]
    [InlineData("js/screens/rutinas.js")]
    [InlineData("js/screens/chat.js")]
    public void Pantalla_YaNoEsProximamente(string archivo)
    {
        var pantalla = Archivos(".js").Single(a => a.Ruta == archivo);

        Assert.DoesNotContain("renderPending", pantalla.Contenido);
    }

    // Cada pantalla usa los endpoints de su historia y muestra lo que la API pide mostrar.
    [Theory]
    [InlineData("js/screens/rutinas.js", "\"/api/exercises\"")]
    [InlineData("js/screens/rutinas.js", "`/api/routines/${")]
    [InlineData("js/screens/rutinas.js", "\"/api/workouts\"")]
    [InlineData("js/screens/rutinas.js", "`/api/workouts?from=${")]
    [InlineData("js/screens/rutinas.js", ".estimatedCaloriesBurned")]
    [InlineData("js/screens/rutinas.js", ".disclaimer")]
    [InlineData("js/screens/chat.js", "\"/api/assistant/conversations\"")]
    [InlineData("js/screens/chat.js", "`/api/assistant/conversations/${")]
    [InlineData("js/screens/chat.js", "`/api/assistant/suggestions/${")]
    [InlineData("js/screens/chat.js", ".degraded")]
    [InlineData("js/screens/chat.js", "maxlength: 1000")]
    [InlineData("js/screens/chat.js", "Estimación, no es consejo médico")]
    [InlineData("js/screens/dia.js", "`/api/summary/weekly?endDate=${")]
    [InlineData("js/screens/dia.js", ".streak")]
    [InlineData("js/screens/dia.js", "`/api/meals/export?from=${")]
    [InlineData("js/screens/dia.js", "\"/api/meals/copy\"")]
    [InlineData("js/screens/dia.js", "Copiar del día anterior")]
    [InlineData("js/screens/alimentos.js", "\"/api/foods/custom\"")]
    [InlineData("js/screens/alimentos.js", "`/api/foods/custom/${")]
    [InlineData("js/screens/alimentos.js", "error.status === 409")]
    [InlineData("js/screens/recetas.js", "\"/api/recipes\"")]
    [InlineData("js/screens/recetas.js", "`/api/recipes/${")]
    [InlineData("js/screens/recetas.js", "`/api/foods/search?q=${")]
    [InlineData("js/screens/recetas.js", "\"/api/meals/recipe\"")]
    [InlineData("js/screens/recetas.js", "`ingredients[${i}].foodId`")]
    [InlineData("js/screens/recetas.js", "`ingredients[${i}].quantityGrams`")]
    [InlineData("js/screens/recetas.js", "MEAL_TYPES")]
    [InlineData("js/screens/recetas.js", "profileTimeZone()")]
    [InlineData("js/screens/recetas.js", "Estimación, no es consejo médico.")]
    public void Pantalla_UsaSusEndpoints(string archivo, string texto)
    {
        var pantalla = Archivos(".js").Single(a => a.Ruta == archivo);

        Assert.Contains(texto, pantalla.Contenido);
    }

    // Las pantallas de L12 están en el menú, antes de Cuenta.
    [Theory]
    [InlineData("{ path: \"/alimentos\", screen: \"alimentos\", title: \"Mis alimentos\", auth: true, nav: true }")]
    [InlineData("{ path: \"/recetas\", screen: \"recetas\", title: \"Recetas\", auth: true, nav: true }")]
    public void Rutas_IncluyenLasPantallasDeRecetas(string ruta)
    {
        var routes = Archivos(".js").Single(a => a.Ruta == "js/routes.js").Contenido;

        var posicion = routes.IndexOf(ruta, StringComparison.Ordinal);
        Assert.True(posicion >= 0 && posicion < routes.IndexOf("path: \"/cuenta\"", StringComparison.Ordinal), $"Falta {ruta} antes de Cuenta");
    }

    // Cada botón de una sugerencia llama a su acción: un "Aceptar" que ignora no crea la comida y nadie lo nota.
    [Theory]
    [InlineData("accept", "Aceptar")]
    [InlineData("ignore", "Ignorar")]
    public void Chat_CadaBotonDeSugerenciaUsaSuAccion(string accion, string boton)
    {
        var chat = Archivos(".js").Single(a => a.Ruta == "js/screens/chat.js").Contenido;

        Assert.Matches(new Regex($@"resolve\(`/api/assistant/suggestions/\$\{{s\.id\}}/{accion}`\)\s*\}}\s*,\s*""{boton}"""), chat);
    }

    // Usos puntuales que un Contains suelto no ata: la condición del aviso degraded, el header del 429 y las kcal
    // del entrenamiento recién guardado.
    [Theory]
    [InlineData("js/screens/chat.js", "if (reply.degraded)")]
    [InlineData("js/api.js", "response.headers.get(\"Retry-After\")")]
    [InlineData("js/screens/rutinas.js", "num(saved.estimatedCaloriesBurned, 0)")]
    [InlineData("js/screens/dia.js", "fromDate: addDays(state.date, -1), toDate: state.date")]
    [InlineData("js/screens/dia.js", "error.status === 409")]
    [InlineData("js/screens/objetivo.js", "rateField.hidden = type.value === \"Maintain\"")]
    [InlineData("js/format.js", "export function plural(")]
    [InlineData("js/screens/rutinas.js", "plural(workouts.length, \"entrenamiento\", \"entrenamientos\")")]
    public void Js_UsoPuntual(string archivo, string texto)
    {
        var js = Archivos(".js").Single(a => a.Ruta == archivo);

        Assert.Contains(texto, js.Contenido);
    }

    // Leyenda del art. 3 de la Resolución 14/2018 de la AAIP, copiada textual de
    // https://www.argentina.gob.ar/normativa/nacional/resoluci%C3%B3n-14-2018-307621/texto (22/10/2026).
    private const string LeyendaAaip =
        "LA AGENCIA DE ACCESO A LA INFORMACIÓN PÚBLICA, en su carácter de Órgano de Control de la Ley N° 25.326, " +
        "tiene la atribución de atender las denuncias y reclamos que interpongan quienes resulten afectados en sus " +
        "derechos por incumplimiento de las normas vigentes en materia de protección de datos personales";

    // El párrafo del asistente, no la mención de Gemini en la Finalidad: los tests de texto y de posición se anclan acá.
    private const string AvisoGemini = "El asistente usa un servicio externo de inteligencia artificial (Google Gemini).";

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
    [InlineData(AvisoGemini)]
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

        var gemini = consent.IndexOf(AvisoGemini, StringComparison.Ordinal);
        var derechos = consent.IndexOf("Tus derechos", StringComparison.Ordinal);
        Assert.True(gemini >= 0 && gemini < derechos, $"El aviso de Gemini en {gemini}, Tus derechos en {derechos}");
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
