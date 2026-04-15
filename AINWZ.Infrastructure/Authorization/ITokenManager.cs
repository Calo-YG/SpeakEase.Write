using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace SpeakEase.Authorization.Authorization
{
    /// <summary>
    /// token 管理
    /// </summary>
    public interface ITokenManager
    {
        /// <summary>
        /// 生成JWT Token
        /// </summary>
        /// <param name="claims"></param>
        /// <returns></returns>
        string GenerateAccessToken(IEnumerable<Claim> claims);

        /// <summary>
        /// 生成刷新token
        /// </summary>
        /// <returns></returns>
        string GenerateRefreshToken();

        /// <summary>
        /// 读取token
        /// </summary>
        /// <returns></returns>
        JwtSecurityToken ReadCurrentToken();

        /// <summary>
        /// 验证token有效性并返回 ClaimsPrincipal
        /// </summary>
        /// <returns></returns>
        ClaimsPrincipal ValidateCurrentToken();

        /// <summary>
        /// 验证token有效性并返回 ClaimsPrincipal
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        ClaimsPrincipal ValidateToken(string token);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="token"></param>
        /// <param name="principal"></param>
        /// <returns></returns>
        bool TryValidateToken(string token, out ClaimsPrincipal principal);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        JwtSecurityToken ReadJwtToken(string token);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        TimeSpan? GetRemainingLifetime(string token);
    }
}
