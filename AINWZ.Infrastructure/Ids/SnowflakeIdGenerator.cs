using AINWZ.Infrastructure.Text;

namespace AINWZ.Infrastructure.Ids;

/// <summary>
/// 基于雪花算法的 64 位 ID 生成器。
/// 默认配置更适合单实例场景；多实例部署时应通过配置为不同节点分配不同的 workerId。
/// </summary>
public sealed class SnowflakeIdGenerator : ISnowflakeIdGenerator
{
    private static readonly DateTimeOffset Epoch = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private const int WorkerIdBits = 10;
    private const int SequenceBits = 12;
    private const long MaxWorkerId = (1L << WorkerIdBits) - 1;
    private const long SequenceMask = (1L << SequenceBits) - 1;
    private const int WorkerIdShift = SequenceBits;
    private const int TimestampLeftShift = WorkerIdBits + SequenceBits;

    private readonly object _lock = new();
    private readonly long _workerId;
    private readonly int _maxBackwardMilliseconds;

    private long _lastTimestamp = -1;
    private long _sequence;

    /// <summary>
    /// 初始化雪花 ID 生成器。
    /// </summary>
    /// <param name="workerId">工作节点 ID，范围 0-1023。</param>
    /// <param name="maxBackwardMilliseconds">可容忍的时钟回拨毫秒数。</param>
    public SnowflakeIdGenerator(long workerId, int maxBackwardMilliseconds)
    {
        if (workerId < 0 || workerId > MaxWorkerId)
        {
            throw new ArgumentOutOfRangeException(nameof(workerId), $"workerId 必须在 0 到 {MaxWorkerId} 之间。");
        }

        if (maxBackwardMilliseconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBackwardMilliseconds), "maxBackwardMilliseconds 不能小于 0。");
        }

        _workerId = workerId;
        _maxBackwardMilliseconds = maxBackwardMilliseconds;
    }

    /// <inheritdoc />
    public long NextId()
    {
        lock (_lock)
        {
            var timestamp = GetCurrentTimestamp();

            if (timestamp < _lastTimestamp)
            {
                var backwardMilliseconds = _lastTimestamp - timestamp;
                if (backwardMilliseconds > _maxBackwardMilliseconds)
                {
                    throw new InvalidOperationException($"系统时钟回拨 {backwardMilliseconds}ms，超过允许阈值 {_maxBackwardMilliseconds}ms，无法生成雪花 ID。");
                }

                timestamp = WaitUntil(_lastTimestamp);
            }

            if (timestamp == _lastTimestamp)
            {
                _sequence = (_sequence + 1) & SequenceMask;
                if (_sequence == 0)
                {
                    timestamp = WaitNextTimestamp(_lastTimestamp);
                }
            }
            else
            {
                _sequence = 0;
            }

            _lastTimestamp = timestamp;

            return (timestamp << TimestampLeftShift)
                   | (_workerId << WorkerIdShift)
                   | _sequence;
        }
    }

    /// <inheritdoc />
    public string NextIdString()
    {
        return LongToStringConverter.Convert(NextId());
    }

    private static long GetCurrentTimestamp()
    {
        return (long)(DateTimeOffset.UtcNow - Epoch).TotalMilliseconds;
    }

    private static long WaitNextTimestamp(long lastTimestamp)
    {
        return WaitUntil(lastTimestamp + 1);
    }

    private static long WaitUntil(long targetTimestamp)
    {
        var timestamp = GetCurrentTimestamp();
        while (timestamp < targetTimestamp)
        {
            timestamp = GetCurrentTimestamp();
        }

        return timestamp;
    }
}
