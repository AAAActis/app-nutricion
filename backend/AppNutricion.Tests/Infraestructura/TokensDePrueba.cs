using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AppNutricion.Tests.Infraestructura;

// Token válido para la API de tests, emitido con la hora real (JwtBearer valida con la hora real).
public static class TokensDePrueba
{
    public static string Para(int userId) =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, userId.ToString())]),
            Issuer = "AppNutricion.Api",
            Audience = "AppNutricion.Clientes",
            Expires = DateTime.UtcNow.AddMinutes(10),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ApiFactory.ClaveJwtFalsa)), SecurityAlgorithms.HmacSha256),
        });
}
