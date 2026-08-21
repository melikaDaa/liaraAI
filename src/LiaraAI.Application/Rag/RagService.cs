using System.Diagnostics;
using System.Text;
using LiaraAI.Application.Chat;
using LiaraAI.Application.Search;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LiaraAI.Application.Rag;

/// <summary>
/// Orchestrates the RAG pipeline: query preparation -> search -> context assembly -> LLM call.
/// </summary>
public sealed class RagService : IRagService
{
    private readonly ISearchService _searchService;
    private readonly IChatCompletionService _chatCompletion;
    private readonly IContextBuilder _contextBuilder;
    private readonly IConversationStore _conversationStore;
    private readonly RagOptions _ragOptions;
    private readonly ILogger<RagService> _logger;

    public RagService(
        ISearchService searchService,
        IChatCompletionService chatCompletion,
        IContextBuilder contextBuilder,
        IConversationStore conversationStore,
        IOptions<RagOptions> ragOptions,
        ILogger<RagService> logger)
    {
        _searchService = searchService;
        _chatCompletion = chatCompletion;
        _contextBuilder = contextBuilder;
        _conversationStore = conversationStore;
        _ragOptions = ragOptions.Value;
        _logger = logger;
    }

    public async Task<RagResponse> AskAsync(
        RagRequest request,
        CancellationToken cancellationToken = default)
    {
        var totalSw = Stopwatch.StartNew();
        var conversationId = request.ConversationId ?? Guid.NewGuid().ToString("N");

        _logger.LogInformation(
            "RAG request started. ConversationId={ConversationId}, MessageLength={Length}",
            conversationId, request.Message.Length);

        // Step 1: Input validation.
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            _logger.LogWarning("RAG request with empty message. ConversationId={ConversationId}", conversationId);
            return CreateResponse(
                "لطفاً یک سوال مطرح کنید. / Please ask a question.",
                conversationId);
        }

        if (request.Message.Length > _ragOptions.MaxMessageLength)
        {
            _logger.LogWarning(
                "RAG request message too long ({Length} chars). ConversationId={ConversationId}",
                request.Message.Length, conversationId);
            return CreateResponse(
                $"طول پیام شما بیش از حد مجاز است ({_ragOptions.MaxMessageLength} کاراکتر). لطفاً پیام خود را کوتاه‌تر کنید.",
                conversationId);
        }

        // Step 2: Build the search query with conversation context if available.
        var searchQuery = BuildSearchQuery(request.Message, conversationId);

        // Step 3: Semantic search over documentation chunks.
        var retrievalSw = Stopwatch.StartNew();
        var searchResults = await _searchService.SearchAsync(
            searchQuery,
            _ragOptions.TopK,
            cancellationToken);
        retrievalSw.Stop();

        _logger.LogInformation(
            "Retrieval completed. ConversationId={ConversationId}, QueryLength={QueryLength}, " +
            "RawResults={RawCount}, DurationMs={DurationMs}",
            conversationId, searchQuery.Length, searchResults.Count, retrievalSw.ElapsedMilliseconds);

        // Step 4: Filter by minimum similarity and deduplicate.
        var relevant = DeduplicateAndFilter(searchResults);

        _logger.LogInformation(
            "After filtering. ConversationId={ConversationId}, RelevantResults={Count}, " +
            "TopSimilarity={TopSimilarity:F4}",
            conversationId, relevant.Count,
            relevant.Count > 0 ? relevant[0].Similarity : 0.0);

        // DEBUG: Log detailed retrieval info for development.
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "RAG DEBUG — Query: {Query}", searchQuery);
            _logger.LogDebug(
                "RAG DEBUG — Raw results: {RawCount}, After filter: {FilteredCount}, MinSimilarity: {MinSimilarity}",
                searchResults.Count, relevant.Count, _ragOptions.MinSimilarity);
            for (int i = 0; i < Math.Min(searchResults.Count, 5); i++)
            {
                var r = searchResults[i];
                _logger.LogDebug(
                    "RAG DEBUG — Result[{Index}]: Similarity={Similarity:F4}, Title={Title}, Heading={Heading}",
                    i, r.Similarity, r.DocumentTitle, r.Heading);
            }
        }

        if (relevant.Count == 0)
        {
            _logger.LogInformation(
                "No relevant documentation found. ConversationId={ConversationId}", conversationId);
            return CreateResponse(
                "اطلاعات کافی در مستندات لیارا پیدا نکردم.\n\n" +
                "لطفاً سوال خود را دقیق‌تر مطرح کنید اگر فکر می‌کنید اطلاعات مرتبطی در مستندات لیارا وجود دارد.",
                conversationId);
        }

        // Step 5: Build compact context from relevant chunks.
        var context = _contextBuilder.Build(relevant, _ragOptions.MaxContextCharacters);

        _logger.LogInformation(
            "Context built. ConversationId={ConversationId}, ContextLength={Length}",
            conversationId, context.Length);

        // Step 6: Build LLM messages with conversation history.
        var messages = BuildLlmMessages(request.Message, context, conversationId);

        // Step 7: Call LLM for grounded answer.
        string answer;
        var llmSw = Stopwatch.StartNew();
        try
        {
            answer = await _chatCompletion.CompleteAsync(messages, cancellationToken);
        }
        catch (Exception ex)
        {
            llmSw.Stop();
            _logger.LogError(ex,
                "LLM call failed. ConversationId={ConversationId}, DurationMs={DurationMs}",
                conversationId, llmSw.ElapsedMilliseconds);
            return CreateResponse(
                "متأسفانه خطایی در اتصال به سرویس هوش مصنوعی رخ داد. لطفاً بعداً دوباره تلاش کنید.",
                conversationId,
                MapToSourceResults(relevant));
        }
        llmSw.Stop();

        // Step 8: Store messages in conversation history.
        _conversationStore.AddMessage(conversationId, "user", request.Message);
        _conversationStore.AddMessage(conversationId, "assistant", answer);

        // Step 9: Map and return response.
        var sources = MapToSourceResults(relevant);

        totalSw.Stop();
        _logger.LogInformation(
            "RAG request completed. ConversationId={ConversationId}, " +
            "RetrievalMs={RetrievalMs}, LlmMs={LlmMs}, TotalMs={TotalMs}, " +
            "SourceCount={SourceCount}",
            conversationId, retrievalSw.ElapsedMilliseconds, llmSw.ElapsedMilliseconds,
            totalSw.ElapsedMilliseconds, sources.Count);

        return new RagResponse
        {
            Answer = answer,
            Sources = sources
        };
    }

    /// <summary>
    /// Build the search query by optionally prepending recent conversation context
    /// so that follow-up questions (e.g., "پس چطور بهش وصل بشم？") resolve correctly.
    /// </summary>
    private string BuildSearchQuery(
        string currentMessage,
        string conversationId)
    {
        var history = _conversationStore.GetHistory(conversationId, _ragOptions.MaxHistoryMessages);

        if (history.Count == 0)
            return currentMessage;

        // Only include the last user message for search query enrichment.
        // We don't send the entire history as the search query.
        var lastUserMessage = history
            .Where(m => m.Role == "user")
            .LastOrDefault();

        if (lastUserMessage is null || lastUserMessage.Content == currentMessage)
            return currentMessage;

        // Combine the previous user question with the current one for better retrieval.
        // This helps with references like "اون روش قبلی" or "همون سرویس".
        return $"{lastUserMessage.Content} {currentMessage}";
    }

    /// <summary>
    /// Build the message list for the LLM including system prompt, conversation history,
    /// documentation context, and the current question.
    /// </summary>
    private List<ChatMessage> BuildLlmMessages(
        string currentMessage,
        string context,
        string conversationId)
    {
        var messages = new List<ChatMessage>
        {
            new("system", _ragOptions.SystemPrompt)
        };

        // Add recent conversation history for context continuity.
        var history = _conversationStore.GetHistory(conversationId, _ragOptions.MaxHistoryMessages);
        foreach (var msg in history)
        {
            messages.Add(new ChatMessage(msg.Role, msg.Content));
        }

        // Add the current question with documentation context.
        var userContent =
            $"## Documentation Context\n\n{context}\n\n" +
            $"## User Question\n\n{currentMessage}";

        messages.Add(new ChatMessage("user", userContent));

        return messages;
    }

    /// <summary>
    /// Deduplicate search results by ChunkId and filter by minimum similarity.
    /// </summary>
    private List<SearchResult> DeduplicateAndFilter(IReadOnlyList<SearchResult> results)
    {
        return results
            .Where(r => r.Similarity >= _ragOptions.MinSimilarity)
            .GroupBy(r => r.ChunkId)
            .Select(g => g.First())
            .ToList();
    }

    private static List<SourceResult> MapToSourceResults(List<SearchResult> relevant)
    {
        return relevant.Select(r => new SourceResult
        {
            Title = r.DocumentTitle,
            Url = r.DocumentUrl,
            Heading = r.Heading,
            HeadingPath = r.HeadingPath,
            Similarity = r.Similarity
        }).ToList();
    }

    private static RagResponse CreateResponse(
        string answer,
        string conversationId,
        IReadOnlyList<SourceResult>? sources = null)
    {
        return new RagResponse
        {
            Answer = answer,
            Sources = sources ?? Array.Empty<SourceResult>()
        };
    }
}
