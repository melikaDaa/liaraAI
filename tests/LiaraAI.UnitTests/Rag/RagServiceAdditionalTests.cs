using LiaraAI.Application.Chat;
using LiaraAI.Application.Rag;
using LiaraAI.Application.Search;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LiaraAI.UnitTests.Rag;

public class ContextBuilderTests
{
    private readonly DocumentationContextBuilder _builder = new();

    [Fact]
    public void Build_returns_empty_for_no_results()
    {
        var result = _builder.Build(Array.Empty<SearchResult>(), 1000);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Build_returns_empty_for_zero_max_characters()
    {
        var results = new List<SearchResult>
        {
            new() { ChunkId = Guid.NewGuid(), Content = "content", DocumentTitle = "Doc", Similarity = 0.9 }
        };
        var result = _builder.Build(results, 0);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Build_formats_source_entry_correctly()
    {
        var results = new List<SearchResult>
        {
            new()
            {
                ChunkId = Guid.NewGuid(),
                Content = "Test content here.",
                Heading = "Databases",
                HeadingPath = "Services > Databases",
                Similarity = 0.9,
                DocumentTitle = "PostgreSQL Guide",
                DocumentUrl = "https://docs.liara.ir/db/pg"
            }
        };

        var context = _builder.Build(results, 10000);

        Assert.Contains("[Source 1]", context);
        Assert.Contains("Title: PostgreSQL Guide", context);
        Assert.Contains("Heading: Databases", context);
        Assert.Contains("HeadingPath: Services > Databases", context);
        Assert.Contains("URL: https://docs.liara.ir/db/pg", context);
        Assert.Contains("Content:", context);
        Assert.Contains("Test content here.", context);
    }

    [Fact]
    public void Build_respects_character_budget()
    {
        var results = new List<SearchResult>
        {
            new()
            {
                ChunkId = Guid.NewGuid(),
                Content = new string('a', 200),
                Similarity = 0.9,
                DocumentId = Guid.NewGuid(),
                DocumentTitle = "First Doc"
            },
            new()
            {
                ChunkId = Guid.NewGuid(),
                Content = new string('b', 200),
                Similarity = 0.8,
                DocumentId = Guid.NewGuid(),
                DocumentTitle = "Second Doc"
            }
        };

        var context = _builder.Build(results, 300);

        Assert.Contains("First Doc", context);
        Assert.DoesNotContain("Second Doc", context);
    }

    [Fact]
    public void Build_deduplicates_by_chunk_id()
    {
        var chunkId = Guid.NewGuid();
        var results = new List<SearchResult>
        {
            new()
            {
                ChunkId = chunkId,
                Content = "content",
                Similarity = 0.9,
                DocumentId = Guid.NewGuid(),
                DocumentTitle = "Doc A"
            },
            new()
            {
                ChunkId = chunkId,
                Content = "duplicate",
                Similarity = 0.85,
                DocumentId = Guid.NewGuid(),
                DocumentTitle = "Doc B"
            }
        };

        var context = _builder.Build(results, 10000);

        Assert.Contains("Doc A", context);
        Assert.DoesNotContain("Doc B", context);
    }

    [Fact]
    public void Build_omits_heading_when_null()
    {
        var results = new List<SearchResult>
        {
            new()
            {
                ChunkId = Guid.NewGuid(),
                Content = "content",
                Similarity = 0.9,
                DocumentId = Guid.NewGuid(),
                DocumentTitle = "Doc",
                Heading = null
            }
        };

        var context = _builder.Build(results, 10000);

        Assert.DoesNotContain("Heading:", context);
    }

    [Fact]
    public void Build_omits_heading_path_when_null()
    {
        var results = new List<SearchResult>
        {
            new()
            {
                ChunkId = Guid.NewGuid(),
                Content = "content",
                Similarity = 0.9,
                DocumentId = Guid.NewGuid(),
                DocumentTitle = "Doc",
                HeadingPath = null
            }
        };

        var context = _builder.Build(results, 10000);

        Assert.DoesNotContain("HeadingPath:", context);
    }

    [Fact]
    public void Build_shows_NA_when_url_is_null()
    {
        var results = new List<SearchResult>
        {
            new()
            {
                ChunkId = Guid.NewGuid(),
                Content = "content",
                Similarity = 0.9,
                DocumentId = Guid.NewGuid(),
                DocumentTitle = "Doc",
                DocumentUrl = null
            }
        };

        var context = _builder.Build(results, 10000);

        Assert.Contains("URL: N/A", context);
    }

    [Fact]
    public void Build_includes_multiple_sources_separated_by_separator()
    {
        var results = new List<SearchResult>
        {
            new()
            {
                ChunkId = Guid.NewGuid(),
                Content = "content1",
                Similarity = 0.9,
                DocumentId = Guid.NewGuid(),
                DocumentTitle = "Doc 1"
            },
            new()
            {
                ChunkId = Guid.NewGuid(),
                Content = "content2",
                Similarity = 0.8,
                DocumentId = Guid.NewGuid(),
                DocumentTitle = "Doc 2"
            }
        };

        var context = _builder.Build(results, 10000);

        Assert.Contains("[Source 1]", context);
        Assert.Contains("[Source 2]", context);
        Assert.Contains("---", context);
    }
}

public class InMemoryConversationStoreTests
{
    [Fact]
    public void GetHistory_returns_empty_for_unknown_conversation()
    {
        var store = new InMemoryConversationStore();
        var result = store.GetHistory("unknown", 10);
        Assert.Empty(result);
    }

    [Fact]
    public void GetHistory_returns_empty_for_empty_conversationId()
    {
        var store = new InMemoryConversationStore();
        var result = store.GetHistory("", 10);
        Assert.Empty(result);
    }

    [Fact]
    public void AddMessage_and_GetHistory_roundtrip()
    {
        var store = new InMemoryConversationStore();
        store.AddMessage("conv1", "user", "hello");
        store.AddMessage("conv1", "assistant", "hi there");

        var history = store.GetHistory("conv1", 10);

        Assert.Equal(2, history.Count);
        Assert.Equal("user", history[0].Role);
        Assert.Equal("hello", history[0].Content);
        Assert.Equal("assistant", history[1].Role);
        Assert.Equal("hi there", history[1].Content);
    }

    [Fact]
    public void GetHistory_returns_most_recent_messages()
    {
        var store = new InMemoryConversationStore();
        for (int i = 0; i < 10; i++)
            store.AddMessage("conv1", "user", $"msg{i}");

        var history = store.GetHistory("conv1", 3);

        Assert.Equal(3, history.Count);
        Assert.Equal("msg7", history[0].Content);
        Assert.Equal("msg8", history[1].Content);
        Assert.Equal("msg9", history[2].Content);
    }

    [Fact]
    public void AddMessage_does_nothing_for_empty_conversationId()
    {
        var store = new InMemoryConversationStore();
        store.AddMessage("", "user", "hello");

        var history = store.GetHistory("", 10);
        Assert.Empty(history);
    }

    [Fact]
    public void Different_conversations_are_isolated()
    {
        var store = new InMemoryConversationStore();
        store.AddMessage("conv1", "user", "hello conv1");
        store.AddMessage("conv2", "user", "hello conv2");

        var h1 = store.GetHistory("conv1", 10);
        var h2 = store.GetHistory("conv2", 10);

        Assert.Single(h1);
        Assert.Single(h2);
        Assert.Contains("conv1", h1[0].Content);
        Assert.Contains("conv2", h2[0].Content);
    }

    [Fact]
    public void ConversationMessage_has_timestamp()
    {
        var store = new InMemoryConversationStore();
        var before = DateTimeOffset.UtcNow;
        store.AddMessage("conv1", "user", "hello");
        var after = DateTimeOffset.UtcNow;

        var history = store.GetHistory("conv1", 10);

        Assert.True(history[0].Timestamp >= before);
        Assert.True(history[0].Timestamp <= after);
    }
}

public class RagServiceAdditionalTests
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
        IConversationStore? conversationStore = null)
    {
        return new RagService(
            search,
            chat,
            new DocumentationContextBuilder(),
            conversationStore ?? new InMemoryConversationStore(),
            Options.Create(options ?? DefaultOptions()),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<RagService>.Instance);
    }

    private sealed class FakeSearchService : ISearchService
    {
        private readonly List<SearchResult> _results;
        public string? LastQuery { get; private set; }

        public FakeSearchService(List<SearchResult> results) => _results = results;

        public Task<IReadOnlyList<SearchResult>> SearchAsync(
            string query, int topK = 5, CancellationToken ct = default)
        {
            LastQuery = query;
            return Task.FromResult<IReadOnlyList<SearchResult>>(_results);
        }
    }

    private sealed class FakeChatCompletion : IChatCompletionService
    {
        private readonly Func<IReadOnlyList<Application.Chat.ChatMessage>, string> _responder;
        public List<List<Application.Chat.ChatMessage>> Calls { get; } = new();
        public bool ShouldFail { get; set; }

        public FakeChatCompletion(Func<IReadOnlyList<Application.Chat.ChatMessage>, string> responder)
            => _responder = responder;

        public Task<string> CompleteAsync(
            IReadOnlyList<Application.Chat.ChatMessage> messages, CancellationToken ct = default)
        {
            Calls.Add(messages.ToList());
            if (ShouldFail)
                throw new HttpRequestException("Simulated LLM failure");
            return Task.FromResult(_responder(messages));
        }
    }

    private static Application.Rag.RagRequest Msg(string text, string? conversationId = null) =>
        new() { Message = text, ConversationId = conversationId };

    [Fact]
    public async Task AskAsync_deduplicates_by_chunk_id()
    {
        var chunkId = Guid.NewGuid();
        var searchResults = new List<SearchResult>
        {
            new()
            {
                ChunkId = chunkId,
                Content = "content",
                Similarity = 0.9,
                DocumentId = Guid.NewGuid(),
                DocumentTitle = "Doc"
            },
            new()
            {
                ChunkId = chunkId,
                Content = "content duplicate",
                Similarity = 0.85,
                DocumentId = Guid.NewGuid(),
                DocumentTitle = "Doc Duplicate"
            }
        };

        var search = new FakeSearchService(searchResults);
        var chat = new FakeChatCompletion(_ => "answer");
        var svc = CreateService(search, chat);

        var result = await svc.AskAsync(Msg("dedup test"));

        Assert.Single(result.Sources);
    }

    [Fact]
    public async Task AskAsync_includes_heading_path_in_sources()
    {
        var searchResults = new List<SearchResult>
        {
            new()
            {
                ChunkId = Guid.NewGuid(),
                Content = "content",
                Heading = "Backups",
                HeadingPath = "Databases > PostgreSQL > Backups",
                Similarity = 0.9,
                DocumentId = Guid.NewGuid(),
                DocumentTitle = "PG Backups"
            }
        };

        var search = new FakeSearchService(searchResults);
        var chat = new FakeChatCompletion(_ => "answer");
        var svc = CreateService(search, chat);

        var result = await svc.AskAsync(Msg("heading path test"));

        Assert.Single(result.Sources);
        Assert.Equal("Databases > PostgreSQL > Backups", result.Sources[0].HeadingPath);
    }

    [Fact]
    public async Task AskAsync_returns_message_when_message_too_long()
    {
        var opts = DefaultOptions();
        opts.MaxMessageLength = 100;
        var search = new FakeSearchService(new());
        var chat = new FakeChatCompletion(_ => "never");
        var svc = CreateService(search, chat, opts);

        var result = await svc.AskAsync(Msg(new string('a', 101)));

        Assert.NotEmpty(result.Answer);
        Assert.Empty(chat.Calls);
    }

    [Fact]
    public async Task AskAsync_uses_conversation_history_for_search_query()
    {
        var store = new InMemoryConversationStore();
        store.AddMessage("conv1", "user", "How to deploy Docker?");

        var search = new FakeSearchService(new());
        var chat = new FakeChatCompletion(_ => "ok");
        var svc = CreateService(search, chat, conversationStore: store);

        await svc.AskAsync(Msg("What about env vars?", "conv1"));

        Assert.Contains("How to deploy Docker?", search.LastQuery!);
        Assert.Contains("What about env vars?", search.LastQuery!);
    }

    [Fact]
    public async Task AskAsync_includes_conversation_history_in_llm_messages()
    {
        var store = new InMemoryConversationStore();
        store.AddMessage("conv1", "user", "First question");
        store.AddMessage("conv1", "assistant", "First answer");

        var search = new FakeSearchService(new List<SearchResult>
        {
            new()
            {
                ChunkId = Guid.NewGuid(),
                Content = "doc content",
                Similarity = 0.9,
                DocumentId = Guid.NewGuid(),
                DocumentTitle = "Doc"
            }
        });

        List<Application.Chat.ChatMessage>? capturedMessages = null;
        var chat = new FakeChatCompletion(msgs =>
        {
            capturedMessages = msgs.ToList();
            return "final answer";
        });

        var svc = CreateService(search, chat, conversationStore: store);

        var result = await svc.AskAsync(Msg("Follow up?", "conv1"));

        Assert.NotNull(capturedMessages);
        Assert.Equal(4, capturedMessages!.Count);
        Assert.Equal("system", capturedMessages[0].Role);
        Assert.Equal("user", capturedMessages[1].Role);
        Assert.Equal("First question", capturedMessages[1].Content);
        Assert.Equal("assistant", capturedMessages[2].Role);
        Assert.Equal("First answer", capturedMessages[2].Content);
        Assert.Equal("user", capturedMessages[3].Role);
    }

    [Fact]
    public async Task AskAsync_stores_messages_in_conversation_store()
    {
        var store = new InMemoryConversationStore();
        var search = new FakeSearchService(new List<SearchResult>
        {
            new()
            {
                ChunkId = Guid.NewGuid(),
                Content = "content",
                Similarity = 0.9,
                DocumentId = Guid.NewGuid(),
                DocumentTitle = "Doc"
            }
        });
        var chat = new FakeChatCompletion(_ => "my answer");
        var svc = CreateService(search, chat, conversationStore: store);

        await svc.AskAsync(Msg("my question", "conv1"));

        var history = store.GetHistory("conv1", 10);
        Assert.Equal(2, history.Count);
        Assert.Equal("user", history[0].Role);
        Assert.Equal("my question", history[0].Content);
        Assert.Equal("assistant", history[1].Role);
        Assert.Equal("my answer", history[1].Content);
    }

    [Fact]
    public async Task AskAsync_generates_conversation_id_when_not_provided()
    {
        var search = new FakeSearchService(new List<SearchResult>
        {
            new()
            {
                ChunkId = Guid.NewGuid(),
                Content = "content",
                Similarity = 0.9,
                DocumentId = Guid.NewGuid(),
                DocumentTitle = "Doc"
            }
        });
        var chat = new FakeChatCompletion(_ => "answer");
        var svc = CreateService(search, chat);

        var result = await svc.AskAsync(Msg("test"));

        Assert.NotEmpty(result.Answer);
    }

    [Fact]
    public async Task AskAsync_skips_duplicate_history_messages()
    {
        var store = new InMemoryConversationStore();
        store.AddMessage("conv1", "user", "same question");

        var search = new FakeSearchService(new());
        var chat = new FakeChatCompletion(_ => "ok");
        var svc = CreateService(search, chat, conversationStore: store);

        await svc.AskAsync(Msg("same question", "conv1"));

        Assert.Equal("same question", search.LastQuery!);
    }
}
