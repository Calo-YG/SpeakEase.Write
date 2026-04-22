namespace SpeakEase.Write.Application.Contracts.Users.Dto;

/// <summary>
/// 用户信息响应。
/// </summary>
public sealed class UserResponse
{
    /// <summary>
    /// 用户标识。
    /// </summary>
    public string Id { get; set; }

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
    /// 头像地址。
    /// </summary>
    public string Avatar { get; set; }

    /// <summary>
    /// 用户角色。
    /// </summary>
    public string Role { get; set; }

    /// <summary>
    /// 是否启用。
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// 创建时间。
    /// </summary>
    public DateTime CreateAt { get; set; }
}
