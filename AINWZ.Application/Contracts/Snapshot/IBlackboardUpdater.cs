namespace SpeakEase.Write.Application.Contracts.Snapshot;

public interface IBlackboardUpdater
{
    void UpdateChapterContent(string chapterId, string content, string summary);
    void RemoveChapter(string chapterId);
    void Clear();
}
