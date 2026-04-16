namespace AINWZ.Application.Contracts.Auth.Dto;

/// <summary>
/// 登录请求。
/// </summary>
public sealed class LoginRequest
{
    /// <summary>
    /// 登录账户。
    /// </summary>
    public string Account { get; set; }

    /// <summary>
    /// 密码。
    /// </summary>
    public string Password { get; set; }
}
