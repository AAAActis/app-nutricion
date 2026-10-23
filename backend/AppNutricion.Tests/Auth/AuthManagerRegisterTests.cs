using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.BusinessLogic.Managers;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using AppNutricion.Tests.Infraestructura;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace AppNutricion.Tests.Auth;

public class AuthManagerRegisterTests
{
    private const string PasswordValida = "una-clave-segura";
    private static readonly DateTimeOffset Ahora = new(2026, 10, 21, 15, 30, 0, TimeSpan.Zero);
    private static readonly RelojFijo Reloj = new(Ahora);

    private readonly Mock<IUserAccountStore> _store = new();
    private readonly AuthManager _manager;

    public AuthManagerRegisterTests()
    {
        _store.Setup(s => s.TryAddAsync(It.IsAny<User>())).ReturnsAsync(true);
        _manager = new AuthManager(_store.Object, new PasswordHasher<User>(), Mock.Of<IAccessTokenService>(), Reloj);
    }

    // local (64) + "@" + dominio: total exacto pedido.
    internal static string EmailDeLargo(int largo) =>
        new string('a', 64) + "@" + new string('b', 63) + "." + new string('c', 63) + "." + new string('d', largo - 64 - 1 - 63 - 1 - 63 - 1 - 4) + ".com";

    public static TheoryData<string?> EmailsInvalidos() =>
    [
        null,
        "",
        "   ",
        "sin-arroba.com",
        "ana@",
        "@mail.com",
        "Ana <ana@mail.com>",
        EmailDeLargo(257),
    ];

    [Theory]
    [MemberData(nameof(EmailsInvalidos))]
    public async Task RegisterAsync_EmailInvalido_LanzaArgumentExceptionDeEmail(string? email)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _manager.RegisterAsync(email, PasswordValida, true));

        Assert.Equal("email", ex.ParamName);
        _store.Verify(s => s.TryAddAsync(It.IsAny<User>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1234567")]
    [InlineData(129)]
    public async Task RegisterAsync_PasswordInvalida_LanzaArgumentExceptionDePassword(object? password)
    {
        var valor = password is int largo ? new string('x', largo) : (string?)password;

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _manager.RegisterAsync("ana@mail.com", valor, true));

        Assert.Equal("password", ex.ParamName);
        _store.Verify(s => s.TryAddAsync(It.IsAny<User>()), Times.Never);
    }

    private async Task<(RegisterResult Resultado, User? Guardado)> RegistrarAsync(string email, string password)
    {
        User? guardado = null;
        _store.Setup(s => s.TryAddAsync(It.IsAny<User>())).Callback<User>(u => guardado = u).ReturnsAsync(true);
        var resultado = await _manager.RegisterAsync(email, password, true);
        return (resultado, guardado);
    }

    [Fact]
    public async Task RegisterAsync_DatosValidos_GuardaEmailNormalizadoYDevuelveElUsuario()
    {
        var (resultado, guardado) = await RegistrarAsync("  Ana.Perez@Mail.COM ", PasswordValida);

        Assert.NotNull(guardado);
        Assert.Equal("ana.perez@mail.com", guardado.Email);
        Assert.Equal(RegisterStatus.Registered, resultado.Status);
        Assert.Same(guardado, resultado.User);
    }

    [Fact]
    public async Task RegisterAsync_GuardaHashVerificableQueEntraEnLaColumna()
    {
        var (_, guardado) = await RegistrarAsync("ana@mail.com", PasswordValida);

        Assert.NotNull(guardado);
        Assert.NotEqual(PasswordValida, guardado.PasswordHash);
        Assert.InRange(guardado.PasswordHash.Length, 1, 100);   // varchar(100) en Users.PasswordHash
        var verificacion = new PasswordHasher<User>().VerifyHashedPassword(guardado, guardado.PasswordHash, PasswordValida);
        Assert.Equal(PasswordVerificationResult.Success, verificacion);
    }

    // Las fechas salen del TimeProvider inyectado: el mismo reloj que usa el token.
    [Fact]
    public async Task RegisterAsync_GuardaConsentimientoVersionadoYFechasDelRelojEnUtc()
    {
        var (_, guardado) = await RegistrarAsync("ana@mail.com", PasswordValida);

        Assert.NotNull(guardado);
        Assert.Equal("2026-10-22", guardado.ConsentVersion);   // texto con responsable, derechos y leyenda AAIP
        Assert.Equal(Ahora.UtcDateTime, guardado.CreatedAt);
        Assert.Equal(Ahora.UtcDateTime, guardado.ConsentAcceptedAt);
        Assert.Equal(DateTimeKind.Utc, guardado.CreatedAt.Kind);
        Assert.Equal(DateTimeKind.Utc, guardado.ConsentAcceptedAt.Kind);
    }

    // Bordes aceptados: password de 8 y de 128 caracteres, email de 256.
    [Theory]
    [InlineData(200, 8)]
    [InlineData(256, 128)]
    public async Task RegisterAsync_EnLosBordesAceptados_Registra(int largoEmail, int largoPassword)
    {
        var email = EmailDeLargo(largoEmail);
        Assert.Equal(largoEmail, email.Length);

        var (resultado, guardado) = await RegistrarAsync(email, new string('x', largoPassword));

        Assert.NotNull(guardado);
        Assert.Equal(RegisterStatus.Registered, resultado.Status);
    }

    [Fact]
    public async Task RegisterAsync_EmailYaRegistrado_DevuelveDuplicadoSinGuardar()
    {
        string? buscado = null;
        _store.Setup(s => s.GetByEmailAsync(It.IsAny<string>()))
            .Callback<string>(e => buscado = e)
            .ReturnsAsync(new User { Id = 1, Email = "ana@mail.com" });

        var resultado = await _manager.RegisterAsync(" ANA@Mail.com ", PasswordValida, true);

        Assert.Equal("ana@mail.com", buscado);
        Assert.Equal(RegisterStatus.EmailAlreadyRegistered, resultado.Status);
        Assert.Null(resultado.User);
        _store.Verify(s => s.TryAddAsync(It.IsAny<User>()), Times.Never);
    }

    // Dos registros simultáneos: los dos pasan el GetByEmail y el índice único de Postgres rechaza al segundo.
    [Fact]
    public async Task RegisterAsync_StoreRechazaPorIndiceUnico_DevuelveDuplicado()
    {
        _store.Setup(s => s.TryAddAsync(It.IsAny<User>())).ReturnsAsync(false);

        var resultado = await _manager.RegisterAsync("ana@mail.com", PasswordValida, true);

        Assert.Equal(RegisterStatus.EmailAlreadyRegistered, resultado.Status);
        Assert.Null(resultado.User);
    }

    [Fact]
    public async Task RegisterAsync_SinConsentimiento_LanzaArgumentExceptionDeConsentAccepted()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _manager.RegisterAsync("ana@mail.com", PasswordValida, false));

        Assert.Equal("consentAccepted", ex.ParamName);
        _store.Verify(s => s.TryAddAsync(It.IsAny<User>()), Times.Never);
    }
}
