using LiaraAI.Application.Conversations;
using LiaraAI.Domain.Conversations;

namespace LiaraAI.UnitTests.Conversations;

public class ConversationServiceTests
{
    private sealed class FakeConversationRepository : IConversationRepository
    {
        private readonly List<Conversation> _conversations = new();
        private readonly List<Message> _messages = new();

        public Task<Conversation?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            var conv = _conversations.FirstOrDefault(c => c.Id == id);
            return Task.FromResult(conv);
        }

        public Task<IReadOnlyList<Conversation>> GetAllAsync(CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<Conversation>>(
                _conversations.OrderByDescending(c => c.UpdatedAt).ToList());
        }

        public Task<Conversation> CreateAsync(Conversation conversation, CancellationToken ct = default)
        {
            _conversations.Add(conversation);
            return Task.FromResult(conversation);
        }

        public Task UpdateAsync(Conversation conversation, CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            _conversations.RemoveAll(c => c.Id == id);
            return Task.CompletedTask;
        }

        public Task<Message> AddMessageAsync(Message message, CancellationToken ct = default)
        {
            _messages.Add(message);
            return Task.FromResult(message);
        }

        public Task<IReadOnlyList<Message>> GetMessagesAsync(
            Guid conversationId, int maxMessages, CancellationToken ct = default)
        {
            var msgs = _messages
                .Where(m => m.ConversationId == conversationId)
                .OrderBy(m => m.CreatedAt)
                .Take(maxMessages)
                .ToList();
            return Task.FromResult<IReadOnlyList<Message>>(msgs);
        }
    }

    [Fact]
    public async Task CreateAsync_creates_conversation_with_title()
    {
        var repo = new FakeConversationRepository();
        var service = new ConversationService(repo);

        var conv = await service.CreateAsync("Test Title");

        Assert.NotEqual(Guid.Empty, conv.Id);
        Assert.Equal("Test Title", conv.Title);
    }

    [Fact]
    public async Task CreateAsync_uses_default_title_when_null()
    {
        var repo = new FakeConversationRepository();
        var service = new ConversationService(repo);

        var conv = await service.CreateAsync();

        Assert.Equal("New Conversation", conv.Title);
    }

    [Fact]
    public async Task GetAllAsync_returns_conversations_ordered_by_updated_at()
    {
        var repo = new FakeConversationRepository();
        var service = new ConversationService(repo);

        await service.CreateAsync("First");
        await service.CreateAsync("Second");

        var all = await service.GetAllAsync();

        Assert.Equal(2, all.Count);
        Assert.Equal("Second", all[0].Title);
        Assert.Equal("First", all[1].Title);
    }

    [Fact]
    public async Task DeleteAsync_removes_conversation()
    {
        var repo = new FakeConversationRepository();
        var service = new ConversationService(repo);

        var conv = await service.CreateAsync("To Delete");
        await service.DeleteAsync(conv.Id);

        var all = await service.GetAllAsync();
        Assert.Empty(all);
    }

    [Fact]
    public async Task UpdateTitleAsync_updates_title()
    {
        var repo = new FakeConversationRepository();
        var service = new ConversationService(repo);

        var conv = await service.CreateAsync("Old Title");
        await service.UpdateTitleAsync(conv.Id, "New Title");

        var updated = await service.GetByIdAsync(conv.Id);
        Assert.Equal("New Title", updated!.Title);
    }

    [Fact]
    public void GenerateTitle_truncates_long_messages()
    {
        var longMessage = new string('a', 100);
        var title = ConversationService.GenerateTitle(longMessage);
        Assert.Equal(53, title.Length); // 50 chars + "..."
        Assert.EndsWith("...", title);
    }

    [Fact]
    public void GenerateTitle_returns_short_message_as_is()
    {
        var title = ConversationService.GenerateTitle("Hello");
        Assert.Equal("Hello", title);
    }

    [Fact]
    public void GenerateTitle_handles_empty_input()
    {
        var title = ConversationService.GenerateTitle(string.Empty);
        Assert.Equal("New Conversation", title);
    }
}
