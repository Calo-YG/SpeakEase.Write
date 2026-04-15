namespace SpeakEase.Authorization.Authorization;

public abstract class UserInfomationConst
{
    /// <summary>
    /// claim type for user id
    /// </summary>
    public const string UserId = "USER_ID";
      
    /// <summary>
    /// claim type for user name
    /// </summary>
    public const string UserName = "USER_NAME";
      
    /// <summary>
    /// claim type for user account
    /// </summary>
    public const string UserAccount = "USER_ACCOUNT";
      
    /// <summary>
    /// claim type for organization id
    /// </summary>
    public const string OrganizationId = "ORGANIZATION_ID";
      
    /// <summary>
    /// claim type for organization name
    /// </summary>
    public const string OrganizationName = "ORGANIZATION_NAME";

    /// <summary>
    /// token redis 键值对
    /// </summary>
    public const string RedisTokenKey = "RedisToken_{0}";
    
    /// <summary>
    /// 刷新token redis 键值对
    /// </summary>
    public const string RefreshTokenKey = "RefreshToken_{0}";

    /// <summary>
    /// 获取权限请求头
    /// </summary>
    public const string AuthorizationHeader = "Authorization";

    /// <summary>
    /// token 前缀
    /// </summary>
    public const string TokenPrefix = "Bearer ";
}