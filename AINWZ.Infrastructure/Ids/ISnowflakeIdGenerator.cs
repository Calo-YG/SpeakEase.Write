namespace AINWZ.Infrastructure.Ids;

/// <summary>
/// 雪花 ID 生成器抽象。
/// </summary>
public interface ISnowflakeIdGenerator
{
    /// <summary>
    /// 生成下一个 long 类型雪花 ID。
    /// </summary>
    long NextId();

    /// <summary>
    /// 生成下一个 string 类型雪花 ID。
    /// </summary>
    string NextIdString();
}
