using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using AppNutricion.Tests.Infraestructura;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AppNutricion.Tests.Api;

public class AuthEndpointsTests(ApiFactoryAuth factory) : IClassFixture<ApiFactoryAuth>
{
    private const string Password = "una-clave-segura";
    private readonly HttpClient _client = factory.CreateClient();

    // El store en memoria se comparte entre los tests de la clase: cada test usa su propio email.
    private static string EmailNuevo() => $"u{Guid.NewGuid():N}@mail.com";

    private Task<HttpResponseMessage> RegistrarAsync(string email, string? password = Password, bool consentAccepted = true) =>
        _client.PostAsJsonAsync("/api/auth/register", new { email, password, consentAccepted });

    private Task<HttpResponseMessage> LoginAsync(string email, string password = Password) =>
        _client.PostAsJsonAsync("/api/auth/login", new { email, password });

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    private async Task<(int Id, string Token)> RegistrarYLoguearAsync()
    {
        var email = EmailNuevo();
        var registro = await RegistrarAsync(email);
        Assert.Equal(HttpStatusCode.Created, registro.StatusCode);
        var id = (await JsonAsync(registro)).GetProperty("id").GetInt32();
        var login = await LoginAsync(email);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return (id, (await JsonAsync(login)).GetProperty("accessToken").GetString()!);
    }

    private Task<HttpResponseMessage> GetMeAsync(string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    // ---------- register ----------

    [Fact]
    public async Task Register_DatosValidos_201ConLocationYSoloIdYEmailNormalizado()
    {
        var email = EmailNuevo();

        var response = await RegistrarAsync("  " + email.ToUpperInvariant() + " ");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/api/me", response.Headers.Location?.OriginalString);
        var json = await JsonAsync(response);
        Assert.Equal(["email", "id"], json.EnumerateObject().Select(p => p.Name).Order());
        Assert.True(json.GetProperty("id").GetInt32() > 0);
        Assert.Equal(email, json.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Register_EmailRepetido_409()
    {
        var email = EmailNuevo();
        Assert.Equal(HttpStatusCode.Created, (await RegistrarAsync(email)).StatusCode);

        var response = await RegistrarAsync(email);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    // El índice único de Postgres distingue mayúsculas: el 409 depende de que se normalice antes.
    [Fact]
    public async Task Register_MismoEmailConOtrasMayusculas_409()
    {
        var email = EmailNuevo();
        Assert.Equal(HttpStatusCode.Created, (await RegistrarAsync(email)).StatusCode);

        var response = await RegistrarAsync(email.ToUpperInvariant());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    public static TheoryData<string, string?, bool, string> DatosInvalidos() => new()
    {
        { "sin-arroba.com", Password, true, "email" },
        { "ana@mail.com", "corta", true, "password" },
        { "ana@mail.com", Password, false, "consentAccepted" },
    };

    [Theory]
    [MemberData(nameof(DatosInvalidos))]
    public async Task Register_DatosInvalidos_400ConElCampoQueFalla(string email, string? password, bool consentAccepted, string campo)
    {
        var response = await RegistrarAsync(email, password, consentAccepted);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errores = (await JsonAsync(response)).GetProperty("errors");
        Assert.Equal([campo], errores.EnumerateObject().Select(p => p.Name));
    }

    // ---------- login ----------

    [Fact]
    public async Task Login_CredencialesCorrectas_200ConTokenYExpiracionUtc()
    {
        var email = EmailNuevo();
        await RegistrarAsync(email);

        var response = await LoginAsync(email.ToUpperInvariant());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await JsonAsync(response);
        Assert.Equal(["accessToken", "expiresAt"], json.EnumerateObject().Select(p => p.Name).Order());
        Assert.False(string.IsNullOrEmpty(json.GetProperty("accessToken").GetString()));
        Assert.EndsWith("Z", json.GetProperty("expiresAt").GetString());
    }

    // Contraseña incorrecta y email inexistente responden exactamente igual: no se revela qué emails existen.
    [Fact]
    public async Task Login_PasswordIncorrectaOEmailInexistente_401ConElMismoCuerpo()
    {
        var email = EmailNuevo();
        await RegistrarAsync(email);

        var incorrecta = await LoginAsync(email, "otra-clave-cualquiera");
        var inexistente = await LoginAsync(EmailNuevo());

        Assert.Equal(HttpStatusCode.Unauthorized, incorrecta.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, inexistente.StatusCode);
        Assert.Equal(await CuerpoSinTraceIdAsync(incorrecta), await CuerpoSinTraceIdAsync(inexistente));
    }

    private static async Task<string> CuerpoSinTraceIdAsync(HttpResponseMessage response)
    {
        var json = await JsonAsync(response);
        return string.Join("|", json.EnumerateObject().Where(p => p.Name != "traceId").Select(p => $"{p.Name}={p.Value}"));
    }

    // ---------- /api/me ----------

    [Fact]
    public async Task Me_SinToken_401()
    {
        var response = await GetMeAsync(null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_ConToken_200ConIdYEmailDelUsuarioDelToken()
    {
        var (id, token) = await RegistrarYLoguearAsync();

        var response = await GetMeAsync(token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await JsonAsync(response);
        Assert.Equal(["email", "id"], json.EnumerateObject().Select(p => p.Name).Order());
        Assert.Equal(id, json.GetProperty("id").GetInt32());
    }

    // Nunca se serializa la entidad User: ni el hash ni nada parecido a una contraseña sale en una respuesta.
    [Fact]
    public async Task RegisterYMe_NoDevuelvenNingunCampoDeHashNiPassword()
    {
        var email = EmailNuevo();
        var registro = await RegistrarAsync(email);
        Assert.Equal(HttpStatusCode.Created, registro.StatusCode);
        var token = (await JsonAsync(await LoginAsync(email))).GetProperty("accessToken").GetString();
        var me = await GetMeAsync(token);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);

        foreach (var cuerpo in new[] { await registro.Content.ReadAsStringAsync(), await me.Content.ReadAsStringAsync() })
        {
            Assert.DoesNotContain("hash", cuerpo, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("password", cuerpo, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ---------- tokens inválidos ----------

    private static string TokenPara(int userId, string clave = ApiFactory.ClaveJwtFalsa, string issuer = "AppNutricion.Api",
        string audiencia = "AppNutricion.Clientes", string algoritmo = SecurityAlgorithms.HmacSha256) =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, userId.ToString())]),
            Issuer = issuer,
            Audience = audiencia,
            Expires = DateTime.UtcNow.AddMinutes(10),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(clave)), algoritmo),
        });

    // Control: un token armado a mano con los datos correctos sí entra. Así los 401 de abajo se deben al dato cambiado.
    [Fact]
    public async Task Me_TokenArmadoConLosDatosCorrectos_200()
    {
        var (id, _) = await RegistrarYLoguearAsync();

        var response = await GetMeAsync(TokenPara(id));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // El último: HS512 con la clave correcta. Solo cambia el algoritmo.
    public static TheoryData<string, string, string, string> TokensInvalidos() => new()
    {
        { "otra-clave-de-al-menos-32-bytes-xxxxxx", "AppNutricion.Api", "AppNutricion.Clientes", SecurityAlgorithms.HmacSha256 },
        { ApiFactory.ClaveJwtFalsa, "OtroEmisor", "AppNutricion.Clientes", SecurityAlgorithms.HmacSha256 },
        { ApiFactory.ClaveJwtFalsa, "AppNutricion.Api", "OtraAudiencia", SecurityAlgorithms.HmacSha256 },
        { ApiFactory.ClaveJwtFalsa, "AppNutricion.Api", "AppNutricion.Clientes", SecurityAlgorithms.HmacSha512 },
    };

    [Theory]
    [MemberData(nameof(TokensInvalidos))]
    public async Task Me_TokenConClaveEmisorAudienciaOAlgoritmoDistinto_401(string clave, string issuer, string audiencia, string algoritmo)
    {
        var (id, _) = await RegistrarYLoguearAsync();

        var response = await GetMeAsync(TokenPara(id, clave, issuer, audiencia, algoritmo));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
