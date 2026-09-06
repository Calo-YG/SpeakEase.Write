using SpeakEase.Write.Application.Contracts.Users.Dto;
using SpeakEase.Write.Application.Shared;

namespace SpeakEase.Write.Application.Contracts.Users
{
    public interface IUserApplication
    {
        /// <summary>
        /// 获取当前用户信息。
        /// </summary>
        Task<ApiResult<UserResponse>> GetProfileAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 更新当前用户资料。
        /// </summary>
        Task<ApiResult<UserResponse>> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// 修改当前用户密码。
        /// </summary>
        Task<ApiResult> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken = default);
    }
}
