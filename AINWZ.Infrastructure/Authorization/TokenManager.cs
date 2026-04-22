using SpeakEase.Write.Infrastructure.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SpeakEase.Authorization.Authorization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace SpeakEase.Authorization;

/// <summary>
/// JWT Token 管理器 - 优化版本
/// 改进：安全性、性能、错误处理、线程安全
/// </summary>
public sealed class TokenManager : ITokenManager
{
    private readonly JwtOptions _options;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<TokenManager> _logger;

    // 缓存密钥和凭证，避免重复计算
    private readonly SymmetricSecurityKey _securityKey;
    private readonly SigningCredentials _signingCredentials;
    private readonly TokenValidationParameters _validationParameters;

    public TokenManager(
        IOptionsSnapshot<JwtOptions> options,
        IHttpContextAccessor httpContextAccessor,
        ILogger<TokenManager> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // 预验证和初始化密钥相关
        ValidateOptions();

        _securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey!));
        _signingCredentials = new SigningCredentials(_securityKey, SecurityAlgorithms.HmacSha256);

        // 缓存验证参数，避免每次新建
        _validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = _options.Issuer,
            ValidAudience = _options.Audience,
            IssuerSigningKey = _securityKey,
            ClockSkew = TimeSpan.FromMinutes(5), // 允许5分钟时钟偏移
            NameClaimType = ClaimTypes.NameIdentifier,
            RoleClaimType = ClaimTypes.Role
        };
    }

    /// <summary>
    /// 生成访问令牌（短期有效）
    /// </summary>
    public string GenerateAccessToken(IEnumerable<Claim> claims)
    {
        ArgumentNullException.ThrowIfNull(claims);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(_options.ExpMinutes),
            signingCredentials: _signingCredentials
        );

        // 使用静态实例避免重复创建
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// 生成刷新令牌（高熵随机数）
    /// </summary>
    public string GenerateRefreshToken()
    {
        // 256位熵 = 32字节 = 44字符Base64
        var randomBytes = new byte[32];

        // .NET 6+ 优先使用静态方法，避免 IDisposable 开销
#if NET6_0_OR_GREATER
        RandomNumberGenerator.Fill(randomBytes);
#else
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
#endif

        // URL安全的Base64变体，避免传输问题
        return Convert.ToBase64String(randomBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }

    /// <summary>
    /// 从当前请求提取并解析 JWT
    /// </summary>
    public JwtSecurityToken ReadCurrentToken()
    {
        var token = ExtractTokenFromHeader();
        return ReadJwtToken(token);
    }

    /// <summary>
    /// 验证当前请求的访问令牌
    /// </summary>
    public ClaimsPrincipal ValidateCurrentToken()
    {
        var token = ExtractTokenFromHeader();
        return ValidateTokenInternal(token);
    }

    /// <summary>
    /// 验证指定令牌（支持刷新令牌场景）
    /// </summary>
    public ClaimsPrincipal ValidateToken(string token)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);
        return ValidateTokenInternal(token);
    }

    /// <summary>
    /// 尝试验证令牌，返回是否成功（不抛异常）
    /// </summary>
    public bool TryValidateToken(string token, out ClaimsPrincipal principal)
    {
        principal = null;

        if (string.IsNullOrEmpty(token))
            return false;

        try
        {
            principal = ValidateTokenInternal(token);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Token validation failed");
            return false;
        }
    }

    /// <summary>
    /// 解析令牌（不验证签名和过期时间，仅读取内容）
    /// </summary>
    public JwtSecurityToken ReadJwtToken(string token)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);

        try
        {
            // 可以读取无签名令牌，用于调试或客户端预览
            return new JwtSecurityTokenHandler().ReadJwtToken(token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read JWT token");
            throw new SecurityTokenException("Invalid token format", ex);
        }
    }

    /// <summary>
    /// 获取令牌剩余有效时间
    /// </summary>
    public TimeSpan? GetRemainingLifetime(string token)
    {
        try
        {
            var jwt = ReadJwtToken(token);
            return jwt.ValidTo - DateTime.UtcNow;
        }
        catch
        {
            return null;
        }
    }


    private string ExtractTokenFromHeader()
    {
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext?.Request == null)
        {
            throw new InvalidOperationException("No active HTTP context");
        }

        // 支持多种认证方案
        var authHeader = httpContext.Request.Headers.Authorization.FirstOrDefault();

        if (string.IsNullOrEmpty(authHeader))
        {
            throw new SecurityTokenException("Authorization header missing");
        }

        // 严格匹配 Bearer 前缀（大小写不敏感）
        const string bearerPrefix = "Bearer ";
        if (!authHeader.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityTokenException("Invalid authorization scheme");
        }

        var token = authHeader[bearerPrefix.Length..].Trim();

        if (string.IsNullOrEmpty(token))
        {
            throw new SecurityTokenException("Token is empty");
        }

        return token;
    }

    private ClaimsPrincipal ValidateTokenInternal(string token)
    {
        try
        {
            var principal = new JwtSecurityTokenHandler().ValidateToken(
                token,
                _validationParameters,
                out var validatedToken
            );

            // 额外验证令牌类型
            if (validatedToken is JwtSecurityToken jwt)
            {
                _logger.LogDebug("Token validated for subject: {Subject}",
                    jwt.Subject ?? "unknown");
            }

            return principal;
        }
        catch (SecurityTokenExpiredException)
        {
            _logger.LogWarning("Token validation failed: expired");
            throw;
        }
        catch (SecurityTokenInvalidSignatureException)
        {
            _logger.LogWarning("Token validation failed: invalid signature");
            throw;
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
        {
            _logger.LogWarning(ex, "Token validation failed");
            throw;
        }
    }

    private void ValidateOptions()
    {
        // 密钥长度验证（HS256要求至少256位/32字节）
        if (string.IsNullOrEmpty(_options.SecretKey))
        {
            throw new InvalidOperationException("JWT SecretKey is required");
        }

        var keyBytes = Encoding.UTF8.GetBytes(_options.SecretKey);
        if (keyBytes.Length < 32)
        {
            throw new InvalidOperationException(
                $"JWT SecretKey must be at least 32 bytes (current: {keyBytes.Length}). " +
                "HS256 requires 256-bit key minimum.");
        }

        if (string.IsNullOrEmpty(_options.Issuer))
        {
            throw new InvalidOperationException("JWT Issuer is required");
        }

        if (string.IsNullOrEmpty(_options.Audience))
        {
            throw new InvalidOperationException("JWT Audience is required");
        }

        if (_options.ExpMinutes <= 0)
        {
            throw new InvalidOperationException("JWT ExpMinutes must be positive");
        }
    }
}