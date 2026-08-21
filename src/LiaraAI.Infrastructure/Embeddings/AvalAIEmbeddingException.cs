using LiaraAI.Application.Embeddings;

namespace LiaraAI.Infrastructure.Embeddings;

/// <summary>
/// AvalAI-specific embedding exception. Inherits the Application-layer base
/// so the retry orchestrator can check <see cref="EmbeddingProviderException.IsRetryable"/>
/// without crossing the architecture boundary.
/// </summary>
public sealed class AvalAIEmbeddingException : EmbeddingProviderException
{
    public AvalAIEmbeddingException(string message) : base(message)
    {
    }

    public AvalAIEmbeddingException(string message, int statusCode) : base(message, statusCode)
    {
    }

    public AvalAIEmbeddingException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
