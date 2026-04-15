using AINWZ.Infrastructure.Exceptions;
using Microsoft.AspNetCore.Http;
using SpeakEase.Authorization.Authorization;
using System.Security.Claims;

namespace AINWZ.Infrastructure.Authorization;

public class UserContext(IHttpContextAccessor httpContextAccessor) : IUserContext
{
    private readonly IEnumerable<Claim> claims = httpContextAccessor.HttpContext?.User.Claims;

    /// <summary>
    /// 用户id
    /// </summary>
    public string UserId => GetClaimValue(UserInfomationConst.UserId, "Can not get user claims info -- this UserId");

    /// <summary>
    /// 用户名称
    /// </summary>
    public string UserName  => GetClaimValue(UserInfomationConst.UserName, "Can not get user claims info -- this UserName");

    /// <summary>
    /// 用户账号，通常是登录使用的唯一标识符，如手机号、邮箱等。
    /// </summary>
    public string UserAccount => GetClaimValue(UserInfomationConst.UserAccount, "Can not get user claims info -- this UserAccount");


    /// <summary>
    /// 
    /// </summary>
    /// <param name="claimType"></param>
    /// <param name="errorMessage"></param>
    /// <returns></returns>
    private string GetClaimValue(string claimType, string errorMessage)
    {
        var value = claims?.FirstOrDefault(x => x.Type == claimType)?.Value;

        if (string.IsNullOrEmpty(value))
        {
            BusinessThrow.ThrowException(errorMessage);
        }

        return value;
    }
}