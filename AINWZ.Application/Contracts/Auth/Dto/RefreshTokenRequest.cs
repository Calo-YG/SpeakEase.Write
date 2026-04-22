namespace SpeakEase.Write.Application.Contracts.Auth.Dto;

/// <summary>
/// 刷新 Token 请求。
/// </summary>
public sealed class RefreshTokenRequest
{
    /// <summary>
    /// 刷新令牌。
    /// </summary>
    public string RefreshToken { get; set; }
}
