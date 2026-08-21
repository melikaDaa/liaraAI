namespace LiaraAI.Application.Chat;

/// <summary>
/// Provider-agnostic chat completion interface. Implemented in Infrastructure
/// by an AvalAI-backed HTTP client. The Application layer never references
/// the concrete provider.
/// </summary>
public interface IChatCompletionService
{
    /// <summary>
    /// Generate a chat completion for the given messages.
    /// </summary>
    Task<string> CompleteAsync(
        IReadOnlyList<ChatMessage> messages,
        CancellationToken cancellationToken = default);
}
