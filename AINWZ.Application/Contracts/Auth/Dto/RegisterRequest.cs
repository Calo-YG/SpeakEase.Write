namespace AINWZ.Application.Contracts.Auth.Dto;

/// <summary>
/// 注册请求。
/// </summary>
public sealed class RegisterRequest
{
    /// <summary>
    /// 登录账户。
    /// </summary>
    public string Account { get; set; }

    /// <summary>
    /// 邮箱地址。
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// 昵称。
    /// </summary>
    public string NickName { get; set; }

    /// <summary>
    /// 密码。
    /// </summary>
    public string Password { get; set; }
}
