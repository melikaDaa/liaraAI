using LiaraAI.Domain.Conversations;

namespace LiaraAI.Application.Conversations;

public interface IConversationService
{
    Task<Conversation> CreateAsync(string? title = null, CancellationToken ct = default);
    Task<Conversation?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Conversation>> GetAllAsync(CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task UpdateTitleAsync(Guid id, string title, CancellationToken ct = default);
}

public class ConversationService : IConversationService
{
    private readonly IConversationRepository _repository;

    public ConversationService(IConversationRepository repository)
        => _repository = repository;

    public async Task<Conversation> CreateAsync(string? title = null, CancellationToken ct = default)
    {
        var conversation = Conversation.Create(title ?? "New Conversation");
        return await _repository.CreateAsync(conversation, ct);
    }

    public async Task<Conversation?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _repository.GetByIdAsync(id, ct);
    }

    public async Task<IReadOnlyList<Conversation>> GetAllAsync(CancellationToken ct = default)
    {
        return await _repository.GetAllAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await _repository.DeleteAsync(id, ct);
    }

    public async Task UpdateTitleAsync(Guid id, string title, CancellationToken ct = default)
    {
        var conversation = await _repository.GetByIdAsync(id, ct);
        if (conversation != null)
        {
            conversation.Title = title;
            conversation.UpdatedAt = DateTimeOffset.UtcNow;
            await _repository.UpdateAsync(conversation, ct);
        }
    }

    public static string GenerateTitle(string firstMessage, int maxLength = 50)
    {
        if (string.IsNullOrWhiteSpace(firstMessage))
            return "New Conversation";

        var cleaned = firstMessage.Trim();
        if (cleaned.Length <= maxLength)
            return cleaned;

        return cleaned[..maxLength].TrimEnd() + "...";
    }
}
