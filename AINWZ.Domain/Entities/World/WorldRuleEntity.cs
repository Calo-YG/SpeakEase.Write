namespace AINWZ.Domain.Entities.World
{
    /// <summary>
    /// 世界规则实体，用于定义天道法则、限制与特殊机制。
    /// </summary>
    public class WorldRuleEntity : Entity, IOwner
    {
        /// <summary>
        /// 所属世界观标识。
        /// </summary>
        public string WorldSettingId { get; set; } = string.Empty;

        /// <summary>
        /// 所有者标识。
        /// </summary>
        public string OwnerId { get; set; } = string.Empty;

        /// <summary>
        /// 规则名称。
        /// </summary>
        public string RuleName { get; set; } = string.Empty;

        /// <summary>
        /// 规则类型。
        /// </summary>
        public string RuleType { get; set; } = string.Empty;

        /// <summary>
        /// 规则描述。
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 约束配置 JSON。
        /// </summary>
        public string ConstraintJson { get; set; } = string.Empty;
    }
}
