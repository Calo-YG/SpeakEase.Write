namespace AINWZ.Application.Contracts.Auth.Dto
{
    /// <summary>
    /// Token 响应。
    /// </summary>
    public sealed class TokenResponse
    {
        /// <summary>
        /// 访问令牌。
        /// </summary>
        public string AccessToken { get; set; }

        /// <summary>
        /// 刷新令牌。
        /// </summary>
        public string RefreshToken { get; set; }

        /// <summary>
        /// 令牌类型。
        /// </summary>
        public string TokenType { get; set; } = "Bearer";

        /// <summary>
        /// 过期时间（秒）。
        /// </summary>
        public int ExpiresIn { get; set; }
    }
}
