using LiaraAI.Application.Conversations;
using LiaraAI.Domain.Conversations;
using LiaraAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LiaraAI.Infrastructure.Conversations;

public class PostgresConversationRepository : IConversationRepository
{
    private readonly AppDbContext _db;

    public PostgresConversationRepository(AppDbContext db) => _db = db;

    public async Task<Conversation?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Conversations
            .Include(c => c.Messages)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<IReadOnlyList<Conversation>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.Conversations
            .OrderByDescending(c => c.UpdatedAt)
            .ToListAsync(ct);
    }

    public async Task<Conversation> CreateAsync(Conversation conversation, CancellationToken ct = default)
    {
        _db.Conversations.Add(conversation);
        await _db.SaveChangesAsync(ct);
        return conversation;
    }

    public async Task UpdateAsync(Conversation conversation, CancellationToken ct = default)
    {
        _db.Conversations.Update(conversation);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var conversation = await _db.Conversations.FindAsync(new object[] { id }, ct);
        if (conversation != null)
        {
            _db.Conversations.Remove(conversation);
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<Message> AddMessageAsync(Message message, CancellationToken ct = default)
    {
        _db.Messages.Add(message);

        var conversation = await _db.Conversations.FindAsync(
            new object[] { message.ConversationId }, ct);
        if (conversation != null)
        {
            conversation.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        return message;
    }

    public async Task<IReadOnlyList<Message>> GetMessagesAsync(
        Guid conversationId, int maxMessages, CancellationToken ct = default)
    {
        return await _db.Messages
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.CreatedAt)
            .Take(maxMessages)
            .ToListAsync(ct);
    }
}
