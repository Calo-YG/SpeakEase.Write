using SpeakEase.Write.Domain;

namespace SpeakEase.Write.Domain.Entities.Users
{
    /// <summary>
    /// 用户偏好实体，记录创作风格、编辑器偏好与 AI 使用习惯。
    /// </summary>
    public class UserPreferenceEntity : Entity, IOwner
    {
        /// <summary>
        /// 所属用户标识。
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// 所有者标识。
        /// </summary>
        public string OwnerId => UserId;

        /// <summary>
        /// 默认题材。
        /// </summary>
        public string DefaultGenre { get; set; } = string.Empty;

        /// <summary>
        /// 默认叙事风格。
        /// </summary>
        public string NarrativeStyle { get; set; } = string.Empty;

        /// <summary>
        /// 默认文笔风格。
        /// </summary>
        public string WritingStyle { get; set; } = string.Empty;

        /// <summary>
        /// 快捷键与界面偏好 JSON。
        /// </summary>
        public string EditorPreferenceJson { get; set; } = string.Empty;

        /// <summary>
        /// 用户常用提示词字典。
        /// </summary>
        public Dictionary<string, string> PromptPreferences { get; set; } = new();
    }
}
