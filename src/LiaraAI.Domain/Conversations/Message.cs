namespace LiaraAI.Domain.Conversations;

public class Message
{
    public Guid Id { get; init; }
    public Guid ConversationId { get; init; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }

    public Conversation Conversation { get; init; } = null!;

    public static Message Create(Guid conversationId, string role, string content)
    {
        return new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            Role = role,
            Content = content,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
