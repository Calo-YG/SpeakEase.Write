namespace SpeakEase.Write.Application.Abstractions.Ids;

public interface ISnowflakeIdGenerator
{
    long NextId();
    string NextIdString();
}
