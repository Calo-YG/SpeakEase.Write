using SpeakEase.Write.Application.Contracts.Auth.Dto;
using SpeakEase.Write.Infrastructure.Shared;

namespace SpeakEase.Write.Application.Contracts.Auth
{
    /// <summary>
    /// 认证应用服务接口。
    /// </summary>
    public interface IAuthApplication
    {
        /// <summary>
        /// 用户注册。
        /// </summary>
        Task<ApiResult<TokenResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// 用户登录。
        /// </summary>
        Task<ApiResult<TokenResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// 刷新 Token。
        /// </summary>
        Task<ApiResult<TokenResponse>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default);
    }
}
