using SpeakEase.Write.Application.Contracts.AI.Dto;
using SpeakEase.Write.Application.Exceptions;

namespace SpeakEase.Write.Application.Applications;

/// <summary>
/// Chat 入口的统一输入规范化器。只接受客户端 user/assistant 历史，不允许下游自行解释输入。
/// </summary>
public static class AgentInputNormalizer
{
    public static void Normalize(AgentChatRequestDto request)
    {
        if (request is null)
            BusinessThrow.ThrowException("Request cannot be empty.");

        if (string.IsNullOrWhiteSpace(request.WorkId))
            BusinessThrow.ThrowException("WorkId cannot be empty.");

        if (request.Messages == null || request.Messages.Count == 0)
            BusinessThrow.ThrowException("Messages cannot be empty.");

        const int maxMessages = 64;
        const int maxMessageCharacters = 16_000;
        const int maxRequestCharacters = 64_000;

        if (request.Messages.Count > maxMessages)
            BusinessThrow.ThrowException($"Messages cannot contain more than {maxMessages} items.");

        var totalCharacters = 0;
        var hasUserMessage = false;
        foreach (var message in request.Messages)
        {
            if (message is null)
                BusinessThrow.ThrowException("Message cannot be empty.");

            var role = message.Role?.Trim().ToLowerInvariant();
            if (role is not ("user" or "assistant"))
                BusinessThrow.ThrowException("Client message role must be user or assistant.");

            message.Role = role;
            message.Content = NormalizeControls(message.Content);
            if (message.Content.Length > maxMessageCharacters)
                BusinessThrow.ThrowException($"A message cannot exceed {maxMessageCharacters} characters.");

            hasUserMessage |= role == "user" && !string.IsNullOrWhiteSpace(message.Content);
            totalCharacters += message.Content.Length;
        }

        if (!hasUserMessage)
            BusinessThrow.ThrowException("User message cannot be empty.");

        var latestNonEmptyMessage = request.Messages.LastOrDefault(m => !string.IsNullOrWhiteSpace(m.Content));
        if (latestNonEmptyMessage is null || latestNonEmptyMessage.Role != "user")
            BusinessThrow.ThrowException("The latest non-empty message must be a user message.");

        if (totalCharacters > maxRequestCharacters)
            BusinessThrow.ThrowException($"Messages cannot exceed {maxRequestCharacters} characters in total.");

        request.WorkId = NormalizeText(request.WorkId, 128);
        request.SkillName = NormalizeText(request.SkillName, 128);
        request.ClientMessageId = NormalizeText(request.ClientMessageId, 128);
        request.IdempotencyKey = NormalizeText(request.IdempotencyKey, 128);
    }

    private static string NormalizeText(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = NormalizeControls(value);
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static string NormalizeControls(string value)
    {
        return new string((value ?? string.Empty).Trim()
            .Where(c => !char.IsControl(c) || c is '\n' or '\r' or '\t')
            .ToArray());
    }
}
