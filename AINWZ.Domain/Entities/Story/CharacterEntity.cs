using SpeakEase.Write.Domain;

namespace SpeakEase.Write.Domain.Entities.Story
{
    /// <summary>
    /// 角色实体，记录人物基础信息、能力和背景。
    /// </summary>
    public class CharacterEntity : AggregateRootEntity, IOwner
    {
        /// <summary>
        /// 所属作品标识。
        /// </summary>
        public string WorkId { get; set; } = string.Empty;

        /// <summary>
        /// 所有者标识。
        /// </summary>
        public string OwnerId { get; set; } = string.Empty;

        /// <summary>
        /// 角色名称。
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 角色别名。
        /// </summary>
        public string Alias { get; set; } = string.Empty;

        /// <summary>
        /// 角色性别描述。
        /// </summary>
        public string Gender { get; set; } = string.Empty;

        /// <summary>
        /// 年龄描述。
        /// </summary>
        public string AgeDescription { get; set; } = string.Empty;

        /// <summary>
        /// 角色身份。
        /// </summary>
        public string Identity { get; set; } = string.Empty;

        /// <summary>
        /// 外貌描述。
        /// </summary>
        public string Appearance { get; set; } = string.Empty;

        /// <summary>
        /// 性格描述。
        /// </summary>
        public string Personality { get; set; } = string.Empty;

        /// <summary>
        /// 背景故事。
        /// </summary>
        public string BackgroundStory { get; set; } = string.Empty;

        /// <summary>
        /// 角色动机。
        /// </summary>
        public string Motivation { get; set; } = string.Empty;

        /// <summary>
        /// 能力说明。
        /// </summary>
        public string AbilityDescription { get; set; } = string.Empty;

        /// <summary>
        /// 角色标签。
        /// </summary>
        public List<string> Tags { get; set; } = new();

        /// <summary>
        /// 扩展元数据。
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new();
    }
}
