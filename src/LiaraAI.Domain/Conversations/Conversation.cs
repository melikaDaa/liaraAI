namespace LiaraAI.Domain.Conversations;

public class Conversation
{
    public Guid Id { get; init; }
    public string Title { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<Message> Messages { get; init; } = new List<Message>();

    public static Conversation Create(string title)
    {
        var now = DateTimeOffset.UtcNow;
        return new Conversation
        {
            Id = Guid.NewGuid(),
            Title = title,
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}
