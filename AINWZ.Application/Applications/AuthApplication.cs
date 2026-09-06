using System.Security.Claims;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using SpeakEase.Write.Application.Contracts.Auth;
using SpeakEase.Write.Application.Contracts.Auth.Dto;
using SpeakEase.Write.Application.Shared;
using SpeakEase.Write.Domain.Entities.Users;
using SpeakEase.Write.Application.Abstractions.Authorization;
using SpeakEase.Write.Application.Abstractions.Caching;
using SpeakEase.Write.Application.Abstractions.Identity;
using SpeakEase.Write.Application.Abstractions.Ids;
using SpeakEase.Write.Application.Abstractions.Persistence;
using SpeakEaseDbContext = SpeakEase.Write.Application.Abstractions.Persistence.IWriteDbContext;

namespace SpeakEase.Write.Application.Applications;

// 认证应用服务：处理用户注册、登录、令牌刷新，含密码哈希和JWT令牌管理
public class AuthApplication(
    SpeakEaseDbContext dbContext,
    ITokenManager tokenManager,
    IMultiCacheService cacheService,
    ISnowflakeIdGenerator snowflakeIdGenerator,
    IOptions<JwtOptions> jwtOptions) : IAuthApplication
{
    // 用户注册：验证参数 → 检查账户唯一性 → 创建用户 → 自动登录返回Token
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
        // DbUpdateException 可能由账户或邮箱唯一约束冲突导致
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return new ApiResult<TokenResponse>("该账户或邮箱已被注册。", 409);
        }

        // 注册成功后自动登录，生成JWT令牌
        var tokenResult = await GenerateTokenResponseAsync(user.Id, user.Account, user.NickName, user.Role);
        return new ApiResult<TokenResponse>(tokenResult);
    }

    // 用户登录：验证账户密码 → 检查激活状态 → 必要时重哈希密码 → 生成Token
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

        // 密码验证成功但算法已过时，使用ExecuteUpdateAsync原子更新密码哈希（不加载实体）
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

    // 刷新令牌：从缓存查找RefreshToken对应的用户 → 验证用户状态 → 发放新Token并移除旧令牌
    public async Task<ApiResult<TokenResponse>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return new ApiResult<TokenResponse>("刷新令牌不能为空。", 400);
        }

        // 从缓存中查找RefreshToken对应的用户ID（缓存key格式：RefreshToken_{token}）
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

    // 生成Token响应：组装Claims → 生成AccessToken和RefreshToken → 将RefreshToken写入缓存
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

        // 将RefreshToken写入缓存（支持内存+Redis双级缓存），过期时间从配置读取
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

    // 验证邮箱格式
    private static bool IsValidEmail(string email) => ValidationHelper.IsValidEmail(email);
}
