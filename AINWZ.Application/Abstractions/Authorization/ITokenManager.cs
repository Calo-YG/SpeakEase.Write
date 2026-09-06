using System.Security.Claims;

namespace SpeakEase.Write.Application.Abstractions.Authorization;

public interface ITokenManager
{
    string GenerateAccessToken(IEnumerable<Claim> claims);
    string GenerateRefreshToken();
}
