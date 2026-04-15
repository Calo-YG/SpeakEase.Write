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
        /// 生成token
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
        /// 解析token
        /// </summary>
        /// <returns></returns>
        void ValidateAccessToken();

        /// <summary>
        /// 获取SecurityToken
        /// </summary>
        /// <returns></returns>
        JwtSecurityToken GetSecurityToken();

        /// <summary>
        /// 校验token
        /// </summary>
        /// <param name="token"></param>
        void ValidateToken(string token);
    }
}
