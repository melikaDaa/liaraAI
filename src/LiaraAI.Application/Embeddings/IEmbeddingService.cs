namespace LiaraAI.Application.Embeddings;

/// <summary>
/// Provider-agnostic embedding generation. Implemented in Infrastructure by an
/// AvalAI-backed HTTP client. The Application/Domain layers never reference the
/// concrete provider.
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Generate one embedding vector per input string, in the same order as the
    /// inputs. Implementations must not silently pad or truncate vectors.
    /// </summary>
    Task<IReadOnlyList<float[]>> EmbedAsync(
        IReadOnlyList<string> inputs,
        CancellationToken cancellationToken = default);
}
