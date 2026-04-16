namespace AINWZ.Application.Contracts.Users.Dto;

/// <summary>
/// 更新用户资料请求。
/// </summary>
public sealed class UpdateProfileRequest
{
    /// <summary>
    /// 昵称。
    /// </summary>
    public string NickName { get; set; }

    /// <summary>
    /// 邮箱地址。
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// 头像地址。
    /// </summary>
    public string Avatar { get; set; }
}
