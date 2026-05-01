using System.Text;
using SpeakEase.Write.Application.Contracts.Users;
using SpeakEase.Write.Application.Contracts.Users.Dto;
using SpeakEase.Write.Application.Shared;
using SpeakEase.Write.Infrastructure.Authorization;
using SpeakEase.Write.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using SpeakEase.Authorization.Authorization;
using SpeakEase.Write.Infrastructure.Shared;

namespace SpeakEase.Write.Application.Applications
{
    /// <summary>
    /// 用户管理应用服务实现。
    /// </summary>
    public class UserApplication(
        SpeakEaseDbContext dbContext,
        IUserContext userContext) : IUserApplication
    {
        /// <inheritdoc />
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

        /// <inheritdoc />
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

            user.NickName = request.NickName;
            user.Email = request.Email;
            user.Avatar = request.Avatar ?? user.Avatar;
            user.UpdateBy = userContext.UserId;
            user.UpdateAt = DateTime.UtcNow;

            await dbContext.SaveChangesAsync(cancellationToken);

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

        /// <inheritdoc />
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

            var user = await dbContext.Users
                .Where(x => x.Id == userContext.UserId)
                .Select(x => new { x.Id, x.Password, x.Salt })
                .FirstOrDefaultAsync(cancellationToken);

            if (user is null)
            {
                return new ApiResult("用户不存在。", 404);
            }

            var oldHashed = HashPassword(request.OldPassword, user.Salt);
            if (oldHashed != user.Password)
            {
                return new ApiResult("旧密码错误。", 400);
            }

            var newSalt = GenerateSalt();
            var newHashed = HashPassword(request.NewPassword, newSalt);

            var entity = await dbContext.Users.FindAsync([user.Id], cancellationToken);
            if (entity is null)
            {
                return new ApiResult("用户不存在。", 404);
            }

            entity.Salt = newSalt;
            entity.Password = newHashed;
            entity.UpdateBy = userContext.UserId;
            entity.UpdateAt = DateTime.UtcNow;

            await dbContext.SaveChangesAsync(cancellationToken);

            return new ApiResult();
        }

        /// <summary>
        /// 生成随机盐值。
        /// </summary>
        private static string GenerateSalt()
        {
            var bytes = new byte[16];
            System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
            return Convert.ToBase64String(bytes);
        }

        /// <summary>
        /// 使用 HMAC-SHA256 加盐哈希密码。
        /// </summary>
        private static string HashPassword(string password, string salt)
        {
            var saltBytes = Convert.FromBase64String(salt);
            using var hmac = new System.Security.Cryptography.HMACSHA256(saltBytes);
            var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashBytes);
        }

        /// <summary>
        /// 验证邮箱格式。
        /// </summary>
        private static bool IsValidEmail(string email) => ValidationHelper.IsValidEmail(email);
    }
}
