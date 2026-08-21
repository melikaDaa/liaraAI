using LiaraAI.Application.Rag;

namespace LiaraAI.Application.Chat;

/// <summary>
/// Response from the chat endpoint. Reuses SourceResult from the Rag namespace.
/// </summary>
public sealed class ChatResponse
{
    public string Answer { get; set; } = string.Empty;
    public string? ConversationId { get; set; }
    public List<SourceResult> Sources { get; set; } = new();
}
