using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.BusinessLogic.Options;
using AppNutricion.Entities.Entities;
using Microsoft.Extensions.Options;

namespace AppNutricion.BusinessLogic.Services;

// C0 (contrato): sin lógica todavía. Se completa con TDD en C4.
public class JwtAccessTokenService(IOptions<JwtOptions> options, TimeProvider timeProvider) : IAccessTokenService
{
    public LoginResult CreateFor(User user) => new("", default);
}
