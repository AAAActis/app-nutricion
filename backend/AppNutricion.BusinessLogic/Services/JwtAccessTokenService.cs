using System.Security.Claims;
using System.Text;
using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.BusinessLogic.Options;
using AppNutricion.Entities.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AppNutricion.BusinessLogic.Services;

// Emite el access token (JWT HMAC-SHA256). Solo lleva el userId en "sub": nada de email ni nombre.
public class JwtAccessTokenService(IOptions<JwtOptions> options, TimeProvider timeProvider) : IAccessTokenService
{
    public LoginResult CreateFor(User user)
    {
        var jwt = options.Value;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var expiresAt = now.AddMinutes(jwt.ExpirationMinutes);

        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString())]),
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expiresAt,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)), SecurityAlgorithms.HmacSha256),
        });

        return new LoginResult(token, expiresAt);
    }
}
