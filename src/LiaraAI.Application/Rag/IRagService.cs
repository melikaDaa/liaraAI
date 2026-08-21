namespace LiaraAI.Application.Rag;

/// <summary>
/// Provider-agnostic RAG service. Retrieves context, calls the LLM,
/// and returns an answer grounded in documentation.
/// </summary>
public interface IRagService
{
    /// <summary>
    /// Process a user question through the RAG pipeline and return an
    /// answer plus the source chunks used for grounding.
    /// </summary>
    Task<RagResponse> AskAsync(
        RagRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Request to the RAG pipeline.
/// </summary>
public sealed class RagRequest
{
    public string Message { get; set; } = string.Empty;
    public string? ConversationId { get; set; }
}

/// <summary>
/// Response from the RAG pipeline.
/// </summary>
public sealed class RagResponse
{
    public string Answer { get; set; } = string.Empty;
    public IReadOnlyList<SourceResult> Sources { get; set; } = Array.Empty<SourceResult>();
}

/// <summary>
/// A source chunk cited in the RAG answer.
/// </summary>
public sealed class SourceResult
{
    public string Title { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? Heading { get; set; }
    public string? HeadingPath { get; set; }
    public double Similarity { get; set; }
}
