namespace SpeakEase.Write.Domain
{
    /// <summary>
    /// 表示实体具备所有者归属能力。
    /// </summary>
    public interface IOwner
    {
        /// <summary>
        /// 所有者标识。
        /// </summary>
        public string OwnerId { get; }
    }
}
