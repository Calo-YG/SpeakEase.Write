namespace AINWZ.Infrastructure.Ids;

/// <summary>
/// 雪花 ID 配置项。
/// </summary>
public sealed class SnowflakeIdOptions
{
    /// <summary>
    /// 配置节名称。
    /// </summary>
    public const string SectionName = "SnowflakeId";

    /// <summary>
    /// 工作节点 ID，范围 0-1023。
    /// 多实例部署时必须为不同节点分配不同值。
    /// </summary>
    public long WorkerId { get; set; } = 1;

    /// <summary>
    /// 可容忍的时钟回拨毫秒数，超过该值则抛异常。
    /// </summary>
    public int MaxBackwardMilliseconds { get; set; } = 5;
}
