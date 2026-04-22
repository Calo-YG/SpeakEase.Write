using SpeakEase.Write.Domain;

namespace SpeakEase.Write.Domain.Entities.Memory
{
    /// <summary>
    /// 上下文组装日志实体，用于记录 AI 请求前的记忆拼装结果与降级过程。
    /// </summary>
    public class ContextAssemblyLogEntity : Entity, IOwner
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
        /// 所属作品标识。
        /// </summary>
        public string WorkId { get; set; } = string.Empty;

        /// <summary>
        /// 所属章节标识。
        /// </summary>
        public string ChapterId { get; set; } = string.Empty;

        /// <summary>
        /// 关联任务标识。
        /// </summary>
        public string TaskId { get; set; } = string.Empty;

        /// <summary>
        /// 上下文组装模式，例如 continue、polish、summary。
        /// </summary>
        public string ContextMode { get; set; } = string.Empty;

        /// <summary>
        /// 关联的记忆快照标识。
        /// </summary>
        public string SnapshotId { get; set; } = string.Empty;

        /// <summary>
        /// 首选模型标识。
        /// </summary>
        public string PrimaryModelId { get; set; } = string.Empty;

        /// <summary>
        /// 备用模型标识。
        /// </summary>
        public string FallbackModelId { get; set; } = string.Empty;

        /// <summary>
        /// 输入 token 数量。
        /// </summary>
        public int InputTokenCount { get; set; }

        /// <summary>
        /// 第一层（核心设定）消耗的 token 数量。
        /// </summary>
        public int CoreSettingTokens { get; set; }

        /// <summary>
        /// 第二层（近期上下文）消耗的 token 数量。
        /// </summary>
        public int RecentContextTokens { get; set; }

        /// <summary>
        /// 第三层（检索增强）消耗的 token 数量。
        /// </summary>
        public int RetrievedContextTokens { get; set; }

        /// <summary>
        /// 选中的记忆片段标识集合 JSON。
        /// </summary>
        public string SelectedChunkIdsJson { get; set; } = string.Empty;

        /// <summary>
        /// 组装摘要。
        /// </summary>
        public string AssemblySummary { get; set; } = string.Empty;

        /// <summary>
        /// 是否使用了降级模型。
        /// </summary>
        public bool UsedFallback { get; set; }
    }
}
