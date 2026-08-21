namespace LiaraAI.Application.Chat;

/// <summary>
/// A message in the chat conversation.
/// </summary>
public sealed record ChatMessage(string Role, string Content);

/// <summary>
/// Request to the chat endpoint.
/// </summary>
public sealed class ChatRequest
{
    /// <summary>The user's question.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Optional conversation identifier for context continuity.</summary>
    public string? ConversationId { get; set; }
}
