using System.Text;
using AppNutricion.BusinessLogic.Options;
using AppNutricion.BusinessLogic.Services;
using AppNutricion.Entities.Entities;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AppNutricion.Tests.Auth;

public class JwtAccessTokenServiceTests
{
    private const string Clave = "clave-de-tests-de-al-menos-32-bytes!!";
    private static readonly DateTimeOffset Ahora = new(2026, 10, 21, 15, 30, 0, TimeSpan.Zero);

    private static readonly JwtOptions Opciones = new()
    {
        Key = Clave,
        Issuer = "AppNutricion.Api",
        Audience = "AppNutricion.Clientes",
        ExpirationMinutes = 60,
    };

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }

    private static JwtAccessTokenService Servicio() =>
        new(Microsoft.Extensions.Options.Options.Create(Opciones), new RelojFijo(Ahora));

    [Fact]
    public void CreateFor_EmiteTokenConSubIssuerAudienciaYExpiracionDeLaConfig()
    {
        var resultado = Servicio().CreateFor(new User { Id = 42, Email = "ana@mail.com" });

        Assert.False(string.IsNullOrEmpty(resultado.AccessToken), "No se emitió token.");
        var token = new JsonWebTokenHandler().ReadJsonWebToken(resultado.AccessToken);
        Assert.Equal("42", token.Subject);
        Assert.Equal("AppNutricion.Api", token.Issuer);
        Assert.Equal(["AppNutricion.Clientes"], token.Audiences);
        Assert.Equal(Ahora.AddMinutes(60).UtcDateTime, token.ValidTo);
        Assert.Equal(Ahora.AddMinutes(60).UtcDateTime, resultado.ExpiresAt);
        Assert.Equal(DateTimeKind.Utc, resultado.ExpiresAt.Kind);
    }

    [Fact]
    public void CreateFor_NoIncluyeElEmailEnElToken()
    {
        var resultado = Servicio().CreateFor(new User { Id = 42, Email = "ana@mail.com" });

        Assert.False(string.IsNullOrEmpty(resultado.AccessToken), "No se emitió token.");
        var token = new JsonWebTokenHandler().ReadJsonWebToken(resultado.AccessToken);
        Assert.DoesNotContain(token.Claims, c => c.Value.Contains("ana@mail.com"));
    }

    [Fact]
    public async Task CreateFor_FirmaConLaClaveConfigurada()
    {
        var resultado = Servicio().CreateFor(new User { Id = 42, Email = "ana@mail.com" });

        Assert.False(string.IsNullOrEmpty(resultado.AccessToken), "No se emitió token.");
        var validacion = await new JsonWebTokenHandler().ValidateTokenAsync(resultado.AccessToken, new TokenValidationParameters
        {
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Clave)),
            ValidIssuer = "AppNutricion.Api",
            ValidAudience = "AppNutricion.Clientes",
            ValidateLifetime = false,   // el reloj del servicio es fijo
        });
        Assert.True(validacion.IsValid, validacion.Exception?.Message);
    }
}
