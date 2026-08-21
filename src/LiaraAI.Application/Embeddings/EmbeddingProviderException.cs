namespace LiaraAI.Application.Embeddings;

/// <summary>
/// Raised when the embedding provider fails or returns an unusable response.
/// Lives in Application so both the orchestrator and the provider implementation
/// can reference it without crossing the architecture boundary.
/// </summary>
public class EmbeddingProviderException : Exception
{
    public int? StatusCode { get; }

    /// <summary>
    /// Whether this failure is likely transient and the caller should retry.
    /// True for 429 (rate limit), 500+ (server error), and network/timeout errors.
    /// False for 400 (bad request), 401 (auth), 403 (forbidden).
    /// </summary>
    public bool IsRetryable { get; }

    public EmbeddingProviderException(string message) : base(message)
    {
        IsRetryable = true;
    }

    public EmbeddingProviderException(string message, int statusCode) : base(message)
    {
        StatusCode = statusCode;
        IsRetryable = statusCode == 429 || statusCode >= 500;
    }

    public EmbeddingProviderException(string message, Exception innerException)
        : base(message, innerException)
    {
        IsRetryable = true;
    }
}
