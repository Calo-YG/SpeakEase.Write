namespace AINWZ.Application.Contracts.Users.Dto;

/// <summary>
/// 修改密码请求。
/// </summary>
public sealed class ChangePasswordRequest
{
    /// <summary>
    /// 旧密码。
    /// </summary>
    public string OldPassword { get; set; }

    /// <summary>
    /// 新密码。
    /// </summary>
    public string NewPassword { get; set; }
}
