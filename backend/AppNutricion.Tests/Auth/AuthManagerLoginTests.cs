using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.BusinessLogic.Managers;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace AppNutricion.Tests.Auth;

public class AuthManagerLoginTests
{
    private const string Password = "una-clave-segura";
    private static readonly User Usuario = new() { Id = 7, Email = "ana@mail.com", PasswordHash = "hash-guardado" };
    private static readonly LoginResult TokenEmitido = new("token-de-prueba", new DateTime(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc));

    private readonly Mock<IUserAccountStore> _store = new();
    private readonly Mock<IPasswordHasher<User>> _hasher = new();
    private readonly Mock<IAccessTokenService> _tokens = new();
    private readonly AuthManager _manager;
    private int _verificaciones;
    private string? _emailBuscado;

    public AuthManagerLoginTests()
    {
        _store.Setup(s => s.GetByEmailAsync(It.IsAny<string>()))
            .Callback<string>(e => _emailBuscado = e)
            .ReturnsAsync((string e) => e == Usuario.Email ? Usuario : null);
        _tokens.Setup(t => t.CreateFor(Usuario)).Returns(TokenEmitido);
        _manager = new AuthManager(_store.Object, _hasher.Object, _tokens.Object, TimeProvider.System);
    }

    private void HasherDevuelve(PasswordVerificationResult resultado) =>
        _hasher.Setup(h => h.VerifyHashedPassword(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback(() => _verificaciones++)
            .Returns(resultado);

    [Theory]
    [InlineData(PasswordVerificationResult.Success)]
    [InlineData(PasswordVerificationResult.SuccessRehashNeeded)]
    public async Task LoginAsync_CredencialesCorrectas_DevuelveElTokenDelUsuario(PasswordVerificationResult verificacion)
    {
        HasherDevuelve(verificacion);

        var resultado = await _manager.LoginAsync("ana@mail.com", Password);

        Assert.Equal(TokenEmitido, resultado);
    }

    [Fact]
    public async Task LoginAsync_NormalizaElEmailAntesDeBuscar()
    {
        HasherDevuelve(PasswordVerificationResult.Success);

        var resultado = await _manager.LoginAsync("  ANA@Mail.com ", Password);

        Assert.Equal("ana@mail.com", _emailBuscado);
        Assert.Equal(TokenEmitido, resultado);
    }

    [Fact]
    public async Task LoginAsync_PasswordIncorrecta_DevuelveNullYVerificaUnaVez()
    {
        HasherDevuelve(PasswordVerificationResult.Failed);

        var resultado = await _manager.LoginAsync("ana@mail.com", "otra-clave");

        Assert.Null(resultado);
        Assert.Equal(1, _verificaciones);
        _tokens.Verify(t => t.CreateFor(It.IsAny<User>()), Times.Never);
    }

    // Con un email inexistente se verifica igual contra un hash de relleno: el tiempo de respuesta
    // no tiene que revelar si el email está registrado.
    [Fact]
    public async Task LoginAsync_EmailInexistente_DevuelveNullYVerificaIgualUnaVez()
    {
        HasherDevuelve(PasswordVerificationResult.Success);   // aunque el hash de relleno "coincidiera", no hay usuario

        var resultado = await _manager.LoginAsync("nadie@mail.com", Password);

        Assert.Null(resultado);
        Assert.Equal(1, _verificaciones);
        _tokens.Verify(t => t.CreateFor(It.IsAny<User>()), Times.Never);
    }

    // Más de 128 caracteres nunca puede ser una contraseña registrada: no se gasta un hash en ella
    // (con contraseñas enormes el hasher sería un vector de DoS).
    [Fact]
    public async Task LoginAsync_PasswordDeMasDe128_DevuelveNullSinLlamarAlHasher()
    {
        HasherDevuelve(PasswordVerificationResult.Success);

        var resultado = await _manager.LoginAsync("ana@mail.com", new string('x', 129));

        Assert.Null(resultado);
        Assert.Equal(0, _verificaciones);
    }

    [Theory]
    [InlineData(null, Password)]
    [InlineData("ana@mail.com", null)]
    [InlineData("", "")]
    public async Task LoginAsync_DatosVacios_DevuelveNull(string? email, string? password)
    {
        HasherDevuelve(PasswordVerificationResult.Success);

        var resultado = await _manager.LoginAsync(email, password);

        Assert.Null(resultado);
        _tokens.Verify(t => t.CreateFor(It.IsAny<User>()), Times.Never);
    }
}
