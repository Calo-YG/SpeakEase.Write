namespace AINWZ.Domain.Entities.Story
{
    /// <summary>
    /// 角色成长线实体，用于记录角色阶段性变化。
    /// </summary>
    public class CharacterArcEntity : Entity, IOwner
    {
        /// <summary>
        /// 所属作品标识。
        /// </summary>
        public string WorkId { get; set; } = string.Empty;

        /// <summary>
        /// 关联角色标识。
        /// </summary>
        public string CharacterId { get; set; } = string.Empty;

        /// <summary>
        /// 所有者标识。
        /// </summary>
        public string OwnerId { get; set; } = string.Empty;

        /// <summary>
        /// 成长阶段顺序。
        /// </summary>
        public int StageOrder { get; set; }

        /// <summary>
        /// 阶段标题。
        /// </summary>
        public string StageTitle { get; set; } = string.Empty;

        /// <summary>
        /// 初始状态。
        /// </summary>
        public string InitialState { get; set; } = string.Empty;

        /// <summary>
        /// 变化后的状态。
        /// </summary>
        public string ChangedState { get; set; } = string.Empty;

        /// <summary>
        /// 触发变化的事件。
        /// </summary>
        public string TriggerEvent { get; set; } = string.Empty;
    }
}
