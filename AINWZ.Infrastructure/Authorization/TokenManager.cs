using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AINWZ.Infrastructure.Authorization;
using AINWZ.Infrastructure.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace SpeakEase.Authorization.Authorization;

/// <summary>
/// token 管理
/// </summary>
/// <param name="options"></param>
/// <param name="httpContextAccessor"></param>
public class TokenManager(IOptionsSnapshot<JwtOptions> options,IHttpContextAccessor httpContextAccessor,ILoggerFactory loggerFactory): ITokenManager
{
    /// <summary>
    /// 获取jwtoptions
    /// </summary>
    private readonly JwtOptions _option = options.Value;

    /// <summary>
    /// 日志
    /// </summary>
    private readonly ILogger _logger = loggerFactory.CreateLogger("TokenManager");

    /// <summary>
    /// Token Handler
    /// </summary>
    private readonly JwtSecurityTokenHandler _securityTokenHandler = new JwtSecurityTokenHandler();

    /// <summary>
    /// 生成token
    /// </summary>
    /// <param name="claims"></param>
    /// <returns></returns>
    public string GenerateAccessToken(IEnumerable<Claim> claims)
    {
        if (string.IsNullOrEmpty(_option.SecretKey) || string.IsNullOrEmpty(_option.Issuer) || string.IsNullOrEmpty(_option.Issuer))
        {
            BusinessThrow.ThrowException("validate jwt options failed");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_option!.SecretKey!));

        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _option.Issuer,
            audience: _option.Audience,
            claims: claims,
            expires: DateTime.Now.AddMinutes(_option.ExpMinutes), // 短有效期
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// 生成refreshtoken
    /// </summary>
    /// <returns></returns>
    public string GenerateRefreshToken()
    {
        var randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    /// <summary>
    /// token
    /// </summary>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public JwtSecurityToken GetSecurityToken()
    {
        var token = GetToken();

        return _securityTokenHandler.ReadJwtToken(token);
    }

    private string GetToken()
    {
        var tryget = httpContextAccessor!.HttpContext!.Request.Headers.TryGetValue(UserInfomationConst.AuthorizationHeader, out var token);

        if (!tryget)
        {
            BusinessThrow.ThrowException("can not get token value");
        }

        return token.ToString().Replace(UserInfomationConst.TokenPrefix,"");
    }

    /// <summary>
    /// 解析token
    /// </summary>
    /// <returns></returns>
    public void ValidateAccessToken()
    {
        var token = GetToken();
        
        ValidateToken( token);
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="token"></param>
    public void ValidateToken(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            BusinessThrow.ThrowException("token is null or empty");
        }

        _securityTokenHandler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = _option.Issuer,
            ValidAudience = _option.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_option.SecretKey)),
        }, out SecurityToken validatedToken);
    }
    
}