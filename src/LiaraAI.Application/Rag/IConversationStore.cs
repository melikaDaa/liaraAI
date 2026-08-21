namespace LiaraAI.Application.Rag;

/// <summary>
/// A single message in conversation history.
/// </summary>
public sealed record ConversationMessage(string Role, string Content, DateTimeOffset Timestamp);

/// <summary>
/// Lightweight store for conversation history. Scoped per conversation ID.
/// Used to maintain recent message context for follow-up questions.
/// </summary>
public interface IConversationStore
{
    /// <summary>
    /// Get recent messages for a conversation, ordered oldest-first.
    /// Returns at most <paramref name="maxMessages"/> items.
    /// </summary>
    IReadOnlyList<ConversationMessage> GetHistory(
        string conversationId,
        int maxMessages);

    /// <summary>
    /// Add a message to the conversation history.
    /// </summary>
    void AddMessage(
        string conversationId,
        string role,
        string content);
}
