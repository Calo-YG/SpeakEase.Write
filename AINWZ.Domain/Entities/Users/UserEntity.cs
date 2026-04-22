using SpeakEase.Write.Domain;

namespace SpeakEase.Write.Domain.Entities.Users
{
    /// <summary>
    /// 平台用户实体，保存账户资料、认证信息与创作身份信息。
    /// </summary>
    public class UserEntity : AggregateRootEntity
    {
        /// <summary>
        /// 登录账户。
        /// </summary>
        public string Account { get; set; } = string.Empty;

        /// <summary>
        /// 邮箱地址。
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// 昵称。
        /// </summary>
        public string NickName { get; set; } = string.Empty;

        /// <summary>
        /// 密码盐值。
        /// </summary>
        public string Salt { get; set; } = string.Empty;

        /// <summary>
        /// 加密后的密码。
        /// </summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// 头像地址。
        /// </summary>
        public string Avatar { get; set; } = string.Empty;

        /// <summary>
        /// 当前订阅套餐标识。
        /// </summary>
        public string SubscriptionPlan { get; set; } = string.Empty;

        /// <summary>
        /// 用户角色，例如 author、admin。
        /// </summary>
        public string Role { get; set; } = "author";

        /// <summary>
        /// 是否启用。
        /// </summary>
        public bool IsActive { get; set; } = true;
    }
}
