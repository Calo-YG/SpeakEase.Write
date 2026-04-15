namespace SpeakEase.Authorization.Authorization;

public interface IUserContext
{
    /// <summary>
    /// 用户id
    /// </summary>
    public string UserId { get; }

    /// <summary>
    /// 用户名称
    /// </summary>
    public string UserName { get; }

    /// <summary>
    /// 用户账号
    /// </summary>
    public string UserAccount { get; }
}