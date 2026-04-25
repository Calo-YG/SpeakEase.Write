using SpeakEase.Write.Domain;

namespace SpeakEase.Write.Domain.Entities.Learning
{
    /// <summary>
    /// 用户段落收藏关联实体，记录用户对参考段落的收藏关系。
    /// </summary>
    public class UserPassageFavoriteEntity : Entity
    {
        /// <summary>
        /// 用户标识。
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// 参考段落标识。
        /// </summary>
        public string PassageId { get; set; } = string.Empty;
    }
}
