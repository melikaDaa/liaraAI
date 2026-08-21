using LiaraAI.Application.Conversations;
using LiaraAI.Domain.Conversations;

namespace LiaraAI.Infrastructure.Conversations;

public class PostgresConversationStore : Application.Rag.IConversationStore
{
    private readonly IConversationRepository _repository;

    public PostgresConversationStore(IConversationRepository repository)
        => _repository = repository;

    public IReadOnlyList<Application.Rag.ConversationMessage> GetHistory(
        string conversationId, int maxMessages)
    {
        if (string.IsNullOrEmpty(conversationId) || !Guid.TryParse(conversationId, out var id))
            return [];

        var messages = _repository.GetMessagesAsync(id, maxMessages).GetAwaiter().GetResult();

        return messages
            .OrderBy(m => m.CreatedAt)
            .Select(m => new Application.Rag.ConversationMessage(
                m.Role, m.Content, m.CreatedAt))
            .ToList();
    }

    public void AddMessage(string conversationId, string role, string content)
    {
        if (string.IsNullOrEmpty(conversationId) || !Guid.TryParse(conversationId, out var id))
            return;

        var message = Message.Create(id, role, content);
        _repository.AddMessageAsync(message).GetAwaiter().GetResult();
    }
}
