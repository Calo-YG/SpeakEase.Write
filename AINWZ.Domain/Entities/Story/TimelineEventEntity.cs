using SpeakEase.Write.Domain;

namespace SpeakEase.Write.Domain.Entities.Story
{
    /// <summary>
    /// 时间线事件实体，用于维护故事时序与关键事件。
    /// </summary>
    public class TimelineEventEntity : Entity, IOwner
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
        /// 关联章节标识。
        /// </summary>
        public string ChapterId { get; set; } = string.Empty;

        /// <summary>
        /// 事件标题。
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// 事件描述。
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 事件发生时间。
        /// </summary>
        public DateTime EventTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 事件类型。
        /// </summary>
        public string EventType { get; set; } = string.Empty;

        /// <summary>
        /// 关联角色标识集合。
        /// </summary>
        public List<string> RelatedCharacterIds { get; set; } = new();
    }
}
