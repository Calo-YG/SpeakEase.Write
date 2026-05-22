using System.Security.Claims;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using SpeakEase.Write.Application.Contracts.Auth;
using SpeakEase.Write.Application.Contracts.Auth.Dto;
using SpeakEase.Write.Application.Shared;
using SpeakEase.Write.Domain.Entities.Users;
using SpeakEase.Write.Infrastructure.Authorization;
using SpeakEase.Write.Infrastructure.Ids;
using SpeakEase.Write.Infrastructure.Persistence;
using SpeakEase.Authorization.Authorization;
using SpeakEase.Write.Infrastructure.MutilCache;
using SpeakEase.Write.Infrastructure.Shared;

namespace SpeakEase.Write.Application.Applications;

/// <summary>
/// 认证应用服务实现。
/// </summary>
public class AuthApplication(
    SpeakEaseDbContext dbContext,
    ITokenManager tokenManager,
    IMultiCacheService cacheService,
    ISnowflakeIdGenerator snowflakeIdGenerator,
    IOptions<JwtOptions> jwtOptions) : IAuthApplication
{
    /// <inheritdoc />
    public async Task<ApiResult<TokenResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Account))
        {
            return new ApiResult<TokenResponse>("账户不能为空。", 400);
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
        {
            return new ApiResult<TokenResponse>("密码不能为空且长度不能少于6位。", 400);
        }

        if (string.IsNullOrWhiteSpace(request.NickName))
        {
            return new ApiResult<TokenResponse>("昵称不能为空。", 400);
        }

        if (string.IsNullOrWhiteSpace(request.Email) || !IsValidEmail(request.Email))
        {
            return new ApiResult<TokenResponse>("邮箱格式不正确。", 400);
        }

        var account = request.Account.Trim();
        var email = request.Email.Trim();

        // 检查账户是否已存在
        var exists = await dbContext.Users.AnyAsync(x => x.Account == account, cancellationToken);
        if (exists)
        {
            return new ApiResult<TokenResponse>("该账户已被注册。", 409);
        }

        // 生成盐值和加密密码
        var salt = PasswordHasher.GenerateSalt();
        var hashedPassword = PasswordHasher.HashPassword(request.Password, salt);

        var user = new UserEntity
        {
            Id = snowflakeIdGenerator.NextIdString(),
            Account = account,
            Email = email,
            NickName = request.NickName,
            Salt = salt,
            Password = hashedPassword,
            Role = "author",
            IsActive = true
        };

        dbContext.Users.Add(user);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return new ApiResult<TokenResponse>("该账户或邮箱已被注册。", 409);
        }

        // 注册成功后自动登录，生成 token
        var tokenResult = await GenerateTokenResponseAsync(user.Id, user.Account, user.NickName, user.Role);
        return new ApiResult<TokenResponse>(tokenResult);
    }

    /// <inheritdoc />
    public async Task<ApiResult<TokenResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Account))
        {
            return new ApiResult<TokenResponse>("账户不能为空。", 400);
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return new ApiResult<TokenResponse>("密码不能为空。", 400);
        }

        var account = request.Account.Trim();

        var user = await dbContext.Users
            .Where(x => x.Account == account)
            .Select(x => new { x.Id, x.Account, x.NickName, x.Password, x.Salt, x.Role, x.IsActive })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return new ApiResult<TokenResponse>("账户或密码错误。", 401);
        }

        if (!user.IsActive)
        {
            return new ApiResult<TokenResponse>("该账户已被禁用。", 403);
        }

        var verification = PasswordHasher.VerifyPassword(request.Password, user.Salt, user.Password);
        if (!verification.IsValid)
        {
            return new ApiResult<TokenResponse>("账户或密码错误。", 401);
        }

        if (verification.NeedsRehash)
        {
            var newSalt = PasswordHasher.GenerateSalt();
            var newHashedPassword = PasswordHasher.HashPassword(request.Password, newSalt);
            await dbContext.Users
                .Where(x => x.Id == user.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Salt, newSalt)
                    .SetProperty(x => x.Password, newHashedPassword)
                    .SetProperty(x => x.UpdateAt, DateTime.Now), cancellationToken);
        }

        var tokenResult = await GenerateTokenResponseAsync(user.Id, user.Account, user.NickName, user.Role);
        return new ApiResult<TokenResponse>(tokenResult);
    }

    /// <inheritdoc />
    public async Task<ApiResult<TokenResponse>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return new ApiResult<TokenResponse>("刷新令牌不能为空。", 400);
        }

        // 从缓存中查找刷新令牌对应的用户信息
        var cacheKey = string.Format(UserInfomationConst.RefreshTokenKey, request.RefreshToken);
        var userId = await cacheService.GetOrSetAsync<string>(cacheKey, () => Task.FromResult<string>(null));

        if (string.IsNullOrEmpty(userId))
        {
            return new ApiResult<TokenResponse>("刷新令牌无效或已过期。", 401);
        }

        // 查询用户信息
        var user = await dbContext.Users
            .Where(x => x.Id == userId)
            .Select(x => new { x.Id, x.Account, x.NickName, x.Role, x.IsActive })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null || !user.IsActive)
        {
            return new ApiResult<TokenResponse>("用户不存在或已被禁用。", 401);
        }

        // 移除旧的刷新令牌
        await cacheService.RemoveAsync(cacheKey);

        // 生成新的 token
        var tokenResult = await GenerateTokenResponseAsync(user.Id, user.Account, user.NickName, user.Role);
        return new ApiResult<TokenResponse>(tokenResult);
    }

    /// <summary>
    /// 生成 Token 响应（AccessToken + RefreshToken），并将 RefreshToken 存入缓存。
    /// </summary>
    private async Task<TokenResponse> GenerateTokenResponseAsync(string userId, string account, string nickName, string role)
    {
        var claims = new List<Claim>
        {
            new(UserInfomationConst.UserId, userId),
            new(UserInfomationConst.UserAccount, account),
            new(UserInfomationConst.UserName, nickName),
            new(ClaimTypes.Role, role)
        };

        var accessToken = tokenManager.GenerateAccessToken(claims);
        var refreshToken = tokenManager.GenerateRefreshToken();

        // 将 RefreshToken 存入缓存，key = RefreshToken_{token}，value = userId
        var cacheKey = string.Format(UserInfomationConst.RefreshTokenKey, refreshToken);
        var refreshExpire = TimeSpan.FromDays(jwtOptions.Value.RefreshExpire);
        await cacheService.RefreshAsync(cacheKey, userId, memoryExpiry: refreshExpire, redisExpiry: refreshExpire);

        return new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = jwtOptions.Value.ExpMinutes * 60
        };
    }

    /// <summary>
    /// 验证邮箱格式。
    /// </summary>
    private static bool IsValidEmail(string email) => ValidationHelper.IsValidEmail(email);
}
