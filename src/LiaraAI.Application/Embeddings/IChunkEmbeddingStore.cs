namespace LiaraAI.Application.Embeddings;

/// <summary>A chunk that still needs an embedding.</summary>
public sealed record PendingChunk(Guid Id, string Content);

/// <summary>
/// Persistence contract for the embedding backfill. Focused on exactly what the
/// embedding use case needs (not a generic repository).
/// </summary>
public interface IChunkEmbeddingStore
{
    /// <summary>Count chunks whose embedding is still NULL.</summary>
    Task<int> CountPendingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stream chunks whose embedding is NULL, in stable id order, in pages of the
    /// requested size. Only NULL-embedding chunks are returned (idempotency).
    /// </summary>
    IAsyncEnumerable<IReadOnlyList<PendingChunk>> GetPendingBatchesAsync(
        int batchSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persist validated embeddings for the given chunk ids. Only rows that still
    /// have a NULL embedding are updated, so concurrent/repeat runs never
    /// overwrite existing vectors. Returns the number of rows updated.
    /// </summary>
    Task<int> SaveEmbeddingsAsync(
        IReadOnlyDictionary<Guid, float[]> embeddings,
        CancellationToken cancellationToken = default);
}
