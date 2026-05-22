using Microsoft.EntityFrameworkCore;

using SpeakEase.Write.Application.Contracts.Users;
using SpeakEase.Write.Application.Contracts.Users.Dto;
using SpeakEase.Write.Application.Shared;
using SpeakEase.Write.Infrastructure.Authorization;
using SpeakEase.Write.Infrastructure.Persistence;
using SpeakEase.Authorization.Authorization;
using SpeakEase.Write.Infrastructure.Shared;

namespace SpeakEase.Write.Application.Applications
{
// 用户管理应用服务：提供当前用户的资料查询、资料更新和密码修改功能
public class UserApplication(
    SpeakEaseDbContext dbContext,
    IUserContext userContext) : IUserApplication
{
    // 获取当前登录用户的个人资料
    public async Task<ApiResult<UserResponse>> GetProfileAsync(CancellationToken cancellationToken = default)
    {
            var user = await dbContext.Users
                .AsNoTracking()
                .Where(x => x.Id == userContext.UserId)
                .Select(x => new UserResponse
                {
                    Id = x.Id,
                    Account = x.Account,
                    Email = x.Email,
                    NickName = x.NickName,
                    Avatar = x.Avatar,
                    Role = x.Role,
                    IsActive = x.IsActive,
                    CreateAt = x.CreateAt
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (user is null)
            {
                return new ApiResult<UserResponse>("用户不存在。", 404);
            }

            return new ApiResult<UserResponse>(user);
        }

    // 更新用户资料：修改昵称、邮箱、头像，邮箱需验证格式和唯一性
    public async Task<ApiResult<UserResponse>> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken cancellationToken = default)
    {
            if (string.IsNullOrWhiteSpace(request.NickName))
            {
                return new ApiResult<UserResponse>("昵称不能为空。", 400);
            }

            if (string.IsNullOrWhiteSpace(request.Email) || !IsValidEmail(request.Email))
            {
                return new ApiResult<UserResponse>("邮箱格式不正确。", 400);
            }

            var user = await dbContext.Users
                .Where(x => x.Id == userContext.UserId)
                .FirstOrDefaultAsync(cancellationToken);

            if (user is null)
            {
                return new ApiResult<UserResponse>("用户不存在。", 404);
            }

            user.NickName = request.NickName.Trim();
            user.Email = request.Email.Trim();
            user.Avatar = request.Avatar ?? user.Avatar;
            user.UpdateBy = userContext.UserId;
            user.UpdateAt = DateTime.Now;

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            // DbUpdateException 通常由唯一索引冲突导致（邮箱已被占用）
            catch (DbUpdateException)
            {
                return new ApiResult<UserResponse>("邮箱已被其他账户使用。", 409);
            }

            return new ApiResult<UserResponse>(new UserResponse
            {
                Id = user.Id,
                Account = user.Account,
                Email = user.Email,
                NickName = user.NickName,
                Avatar = user.Avatar,
                Role = user.Role,
                IsActive = user.IsActive,
                CreateAt = user.CreateAt
            });
        }

    // 修改密码：验证旧密码后生成新盐值和哈希，更新到数据库
    public async Task<ApiResult> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
            if (string.IsNullOrWhiteSpace(request.OldPassword))
            {
                return new ApiResult("旧密码不能为空。", 400);
            }

            if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
            {
                return new ApiResult("新密码不能为空且长度不能少于6位。", 400);
            }

            // 只查询密码验证所需的字段，减少数据传输
            var user = await dbContext.Users
                .Where(x => x.Id == userContext.UserId)
                .Select(x => new { x.Id, x.Password, x.Salt })
                .FirstOrDefaultAsync(cancellationToken);

            if (user is null)
            {
                return new ApiResult("用户不存在。", 404);
            }

            // 验证旧密码（使用盐值+哈希比对）
            if (!PasswordHasher.VerifyPassword(request.OldPassword, user.Salt, user.Password).IsValid)
            {
                return new ApiResult("旧密码错误。", 400);
            }

            // 生成新盐值和密码哈希
            var newSalt = PasswordHasher.GenerateSalt();
            var newHashed = PasswordHasher.HashPassword(request.NewPassword, newSalt);

            // FindAsync 从跟踪缓存获取实体（若已跟踪则不重复查询）
            var entity = await dbContext.Users.FindAsync([user.Id], cancellationToken);
            if (entity is null)
            {
                return new ApiResult("用户不存在。", 404);
            }

            entity.Salt = newSalt;
            entity.Password = newHashed;
            entity.UpdateBy = userContext.UserId;
            entity.UpdateAt = DateTime.Now;

            await dbContext.SaveChangesAsync(cancellationToken);

            return new ApiResult();
        }

    // 验证邮箱格式
    private static bool IsValidEmail(string email) => ValidationHelper.IsValidEmail(email);
    }
}
