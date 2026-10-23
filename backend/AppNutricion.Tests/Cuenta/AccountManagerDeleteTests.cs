using AppNutricion.BusinessLogic.Managers;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using AppNutricion.Presentation.Input;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace AppNutricion.Tests.Cuenta;

public class AccountManagerDeleteTests
{
    private static readonly User Usuario = new() { Id = 7, Email = "ana@mail.com", PasswordHash = "hash-guardado" };

    private readonly Mock<IUserAccountStore> _store = new();
    private readonly Mock<IExportStore> _export = new();
    private readonly Mock<IPasswordHasher<User>> _hasher = new();
    private readonly AccountManager _manager;

    public AccountManagerDeleteTests()
    {
        _store.Setup(s => s.GetByIdAsync(Usuario.Id)).ReturnsAsync(Usuario);
        _hasher.Setup(h => h.VerifyHashedPassword(Usuario, Usuario.PasswordHash, It.IsAny<string>()))
            .Returns(PasswordVerificationResult.Success);
        _manager = new AccountManager(_store.Object, _export.Object, _hasher.Object);
    }

    // Una contraseña de más de 128 no pudo registrarse: se descarta sin hashear (evita DoS con contraseñas enormes).
    [Fact]
    public async Task Delete_ContraseñaDe129_FalseSinHashearNiBorrar()
    {
        var borrada = await _manager.DeleteAsync(Usuario.Id, new string('a', 129));

        Assert.False(borrada);
        _hasher.Verify(h => h.VerifyHashedPassword(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _store.Verify(s => s.DeleteWithDataAsync(It.IsAny<int>()), Times.Never);
    }

    // Borde: 128 es el máximo del registro, así que con 128 se verifica y se borra.
    [Fact]
    public async Task Delete_ContraseñaDe128Correcta_TrueYBorra()
    {
        var borrada = await _manager.DeleteAsync(Usuario.Id, new string('a', 128));

        Assert.True(borrada);
        _store.Verify(s => s.DeleteWithDataAsync(Usuario.Id), Times.Once);
    }

    // MVC, en Trace, loguea los argumentos de la acción con ToString(): el de un record incluye todas sus propiedades.
    [Fact]
    public void DeleteAccountInput_ToString_NoMuestraLaContraseña()
    {
        var texto = new DeleteAccountInput("Clave-Que-No-Se-Loguea-123").ToString();

        Assert.DoesNotContain("Clave-Que-No-Se-Loguea-123", texto);
    }
}
