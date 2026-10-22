using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.BusinessLogic.Managers;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace AppNutricion.Tests.Auth;

public class AuthManagerRegisterTests
{
    private const string PasswordValida = "una-clave-segura";

    private readonly Mock<IUserAccountStore> _store = new();
    private readonly AuthManager _manager;

    public AuthManagerRegisterTests()
    {
        _store.Setup(s => s.TryAddAsync(It.IsAny<User>())).ReturnsAsync(true);
        _manager = new AuthManager(_store.Object, new PasswordHasher<User>(), Mock.Of<IAccessTokenService>());
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

    [Fact]
    public async Task RegisterAsync_SinConsentimiento_LanzaArgumentExceptionDeConsentAccepted()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _manager.RegisterAsync("ana@mail.com", PasswordValida, false));

        Assert.Equal("consentAccepted", ex.ParamName);
        _store.Verify(s => s.TryAddAsync(It.IsAny<User>()), Times.Never);
    }
}
