using LiaraAI.Domain.Conversations;

namespace LiaraAI.Application.Conversations;

public interface IConversationRepository
{
    Task<Conversation?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Conversation>> GetAllAsync(CancellationToken ct = default);
    Task<Conversation> CreateAsync(Conversation conversation, CancellationToken ct = default);
    Task UpdateAsync(Conversation conversation, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<Message> AddMessageAsync(Message message, CancellationToken ct = default);
    Task<IReadOnlyList<Message>> GetMessagesAsync(Guid conversationId, int maxMessages, CancellationToken ct = default);
}
