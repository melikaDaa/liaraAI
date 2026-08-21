namespace LiaraAI.Application.Embeddings;

/// <summary>Summary of an embedding backfill run.</summary>
public sealed record EmbeddingBackfillResult(
    int PendingAtStart,
    int ChunksEmbedded,
    int ChunksFailed,
    int BatchesProcessed,
    int BatchesFailed,
    long DurationMs);

/// <summary>
/// Orchestrates generating embeddings for all chunks whose embedding is NULL.
/// Idempotent: only NULL-embedding chunks are processed and persisted.
/// </summary>
public interface IChunkEmbeddingService
{
    Task<EmbeddingBackfillResult> BackfillAsync(CancellationToken cancellationToken = default);
}
