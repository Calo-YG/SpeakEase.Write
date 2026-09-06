namespace SpeakEase.Write.Application.Abstractions.Identity;

public interface IUserContext
{
    string UserId { get; }
    string UserName { get; }
    string UserAccount { get; }
}
