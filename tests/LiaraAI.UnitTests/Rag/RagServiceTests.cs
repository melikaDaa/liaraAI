using LiaraAI.Application.Chat;
using LiaraAI.Application.Rag;
using LiaraAI.Application.Search;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LiaraAI.UnitTests.Rag;

public class RagServiceTests
{
    private static RagOptions DefaultOptions() => new()
    {
        TopK = 8,
        MinSimilarity = 0.2,
        MaxContextCharacters = 12000,
        MaxHistoryMessages = 6,
        MaxMessageLength = 4000,
        SystemPrompt = "Answer based on documentation."
    };

    private static RagService CreateService(
        ISearchService search,
        IChatCompletionService chat,
        RagOptions? options = null,
        IContextBuilder? contextBuilder = null,
        IConversationStore? conversationStore = null)
    {
        return new RagService(
            search,
            chat,
            contextBuilder ?? new DocumentationContextBuilder(),
            conversationStore ?? new InMemoryConversationStore(),
            Options.Create(options ?? DefaultOptions()),
            NullLogger<RagService>.Instance);
    }

    private sealed class FakeSearchService : ISearchService
    {
        private readonly List<SearchResult> _results;
        public string? LastQuery { get; private set; }
        public int TopKRequested { get; private set; }

        public FakeSearchService(List<SearchResult> results) => _results = results;

        public Task<IReadOnlyList<SearchResult>> SearchAsync(
            string query, int topK = 5, CancellationToken ct = default)
        {
            LastQuery = query;
            TopKRequested = topK;
            return Task.FromResult<IReadOnlyList<SearchResult>>(_results);
        }
    }

    private sealed class FakeChatCompletion : IChatCompletionService
    {
        private readonly Func<IReadOnlyList<ChatMessage>, string>? _responder;
        public List<List<ChatMessage>> Calls { get; } = new();
        public bool ShouldFail { get; set; }

        public FakeChatCompletion(Func<IReadOnlyList<ChatMessage>, string> responder)
            => _responder = responder;

        public Task<string> CompleteAsync(
            IReadOnlyList<ChatMessage> messages, CancellationToken ct = default)
        {
            Calls.Add(messages.ToList());
            if (ShouldFail)
                throw new HttpRequestException("Simulated LLM failure");
            return Task.FromResult(_responder!(messages));
        }
    }

    private static RagRequest Msg(string text, string? conversationId = null) =>
        new() { Message = text, ConversationId = conversationId };

    [Fact]
    public async Task AskAsync_returns_message_when_query_is_empty()
    {
        var search = new FakeSearchService(new());
        var chat = new FakeChatCompletion(_ => "never");
        var svc = CreateService(search, chat);

        var result = await svc.AskAsync(Msg(""));

        Assert.NotEmpty(result.Answer);
        Assert.Empty(result.Sources);
        Assert.Empty(chat.Calls);
    }

    [Fact]
    public async Task AskAsync_returns_no_match_when_no_results_above_threshold()
    {
        var search = new FakeSearchService(new List<SearchResult>
        {
            new() { ChunkId = Guid.NewGuid(), Content = "low", Similarity = 0.1, DocumentTitle = "Doc" }
        });
        var chat = new FakeChatCompletion(_ => "never");
        var svc = CreateService(search, chat);

        var result = await svc.AskAsync(Msg("test question"));

        Assert.NotEmpty(result.Answer);
        Assert.Empty(result.Sources);
        Assert.Empty(chat.Calls);
    }

    [Fact]
    public async Task AskAsync_passes_context_and_returns_answer_with_sources()
    {
        var searchResults = new List<SearchResult>
        {
            new()
            {
                ChunkId = Guid.NewGuid(),
                Content = "PostgreSQL is a database.",
                Heading = "Databases",
                HeadingPath = "Databases > PostgreSQL",
                Similarity = 0.85,
                DocumentId = Guid.NewGuid(),
                DocumentTitle = "PostgreSQL Guide",
                DocumentUrl = "https://docs.liara.ir/db/pg"
            },
            new()
            {
                ChunkId = Guid.NewGuid(),
                Content = "Use connection strings to connect.",
                Heading = "Connection",
                HeadingPath = "Databases > Connection",
                Similarity = 0.72,
                DocumentId = Guid.NewGuid(),
                DocumentTitle = "Connection Guide",
                DocumentUrl = "https://docs.liara.ir/db/conn"
            }
        };

        var search = new FakeSearchService(searchResults);
        var chat = new FakeChatCompletion(msgs =>
        {
            Assert.Equal("Answer based on documentation.", msgs[0].Content);
            Assert.Contains("PostgreSQL is a database.", msgs[1].Content);
            return "Use PostgreSQL with connection strings.";
        });

        var svc = CreateService(search, chat);
        var result = await svc.AskAsync(Msg("How to use PostgreSQL?"));

        Assert.Equal("Use PostgreSQL with connection strings.", result.Answer);
        Assert.Equal(2, result.Sources.Count);
        Assert.Equal("PostgreSQL Guide", result.Sources[0].Title);
        Assert.Equal("https://docs.liara.ir/db/pg", result.Sources[0].Url);
        Assert.Equal("Databases > PostgreSQL", result.Sources[0].HeadingPath);
        Assert.Equal(0.85, result.Sources[0].Similarity);
    }

    [Fact]
    public async Task AskAsync_calls_search_with_correct_topK()
    {
        var opts = DefaultOptions();
        opts.TopK = 10;
        var search = new FakeSearchService(new());
        var chat = new FakeChatCompletion(_ => "ok");
        var svc = CreateService(search, chat, opts);

        await svc.AskAsync(Msg("test"));

        Assert.Equal(10, search.TopKRequested);
    }

    [Fact]
    public async Task AskAsync_returns_error_message_when_llm_fails()
    {
        var searchResults = new List<SearchResult>
        {
            new()
            {
                ChunkId = Guid.NewGuid(),
                Content = "content",
                Similarity = 0.9,
                DocumentId = Guid.NewGuid(),
                DocumentTitle = "Doc"
            }
        };

        var search = new FakeSearchService(searchResults);
        var chat = new FakeChatCompletion(_ => "never") { ShouldFail = true };
        var svc = CreateService(search, chat);

        var result = await svc.AskAsync(Msg("failing question"));

        Assert.NotEmpty(result.Answer);
        Assert.Single(result.Sources);
    }

    [Fact]
    public async Task AskAsync_respects_max_context_characters()
    {
        var opts = DefaultOptions();
        opts.MaxContextCharacters = 50;

        var searchResults = new List<SearchResult>
        {
            new()
            {
                ChunkId = Guid.NewGuid(),
                Content = new string('x', 30),
                Similarity = 0.9,
                DocumentId = Guid.NewGuid(),
                DocumentTitle = "Long Doc"
            },
            new()
            {
                ChunkId = Guid.NewGuid(),
                Content = new string('y', 30),
                Similarity = 0.8,
                DocumentId = Guid.NewGuid(),
                DocumentTitle = "Second Doc"
            }
        };

        var search = new FakeSearchService(searchResults);
        var chat = new FakeChatCompletion(msgs =>
        {
            var userMsg = msgs[1].Content;
            Assert.Contains("x", userMsg);
            Assert.DoesNotContain("y", userMsg);
            return "truncated";
        });

        var svc = CreateService(search, chat, opts);
        var result = await svc.AskAsync(Msg("truncation test"));

        Assert.Equal("truncated", result.Answer);
    }

    [Fact]
    public async Task AskAsync_filters_results_below_min_similarity()
    {
        var searchResults = new List<SearchResult>
        {
            new()
            {
                ChunkId = Guid.NewGuid(),
                Content = "relevant",
                Similarity = 0.9,
                DocumentId = Guid.NewGuid(),
                DocumentTitle = "Relevant Doc"
            },
            new()
            {
                ChunkId = Guid.NewGuid(),
                Content = "irrelevant",
                Similarity = 0.1,
                DocumentId = Guid.NewGuid(),
                DocumentTitle = "Irrelevant Doc"
            }
        };

        var search = new FakeSearchService(searchResults);
        var chat = new FakeChatCompletion(msgs =>
        {
            Assert.Contains("relevant", msgs[1].Content);
            return "answer";
        });

        var svc = CreateService(search, chat);
        var result = await svc.AskAsync(Msg("threshold test"));

        Assert.Single(result.Sources);
        Assert.Equal("Relevant Doc", result.Sources[0].Title);
    }
}
