using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ApplicationTokenManager = SpeakEase.Write.Application.Abstractions.Authorization.ITokenManager;

namespace SpeakEase.Authorization.Authorization;

public interface ITokenManager : ApplicationTokenManager
{
    JwtSecurityToken ReadCurrentToken();
    ClaimsPrincipal ValidateCurrentToken();
    ClaimsPrincipal ValidateToken(string token);
    bool TryValidateToken(string token, out ClaimsPrincipal principal);
    JwtSecurityToken ReadJwtToken(string token);
    TimeSpan? GetRemainingLifetime(string token);
}
