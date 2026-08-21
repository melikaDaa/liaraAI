using System.Collections.Concurrent;

namespace LiaraAI.Application.Rag;

/// <summary>
/// In-memory conversation history store. Suitable for single-server deployments.
/// For multi-server deployments, replace with a Redis-backed implementation.
/// </summary>
public sealed class InMemoryConversationStore : IConversationStore
{
    private readonly ConcurrentDictionary<string, List<ConversationMessage>> _conversations = new();
    private readonly int _maxMessagesPerConversation;

    public InMemoryConversationStore(int maxMessagesPerConversation = 50)
    {
        _maxMessagesPerConversation = maxMessagesPerConversation;
    }

    public IReadOnlyList<ConversationMessage> GetHistory(
        string conversationId,
        int maxMessages)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
            return Array.Empty<ConversationMessage>();

        if (!_conversations.TryGetValue(conversationId, out var messages))
            return Array.Empty<ConversationMessage>();

        lock (messages)
        {
            var take = Math.Min(maxMessages, messages.Count);
            return messages.Skip(messages.Count - take).ToList();
        }
    }

    public void AddMessage(
        string conversationId,
        string role,
        string content)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
            return;

        var messages = _conversations.GetOrAdd(conversationId, _ => new List<ConversationMessage>());

        lock (messages)
        {
            messages.Add(new ConversationMessage(role, content, DateTimeOffset.UtcNow));

            // Trim to prevent unbounded growth.
            if (messages.Count > _maxMessagesPerConversation)
            {
                messages.RemoveRange(0, messages.Count - _maxMessagesPerConversation);
            }
        }
    }
}
