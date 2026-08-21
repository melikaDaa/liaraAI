using LiaraAI.Application.Embeddings;
using LiaraAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace LiaraAI.Infrastructure.Embeddings;

/// <summary>
/// EF Core implementation of <see cref="IChunkEmbeddingStore"/>.
/// All queries filter on Embedding == null so the backfill is idempotent and
/// never overwrites an already-embedded chunk.
/// </summary>
public sealed class EfChunkEmbeddingStore : IChunkEmbeddingStore
{
    private readonly AppDbContext _dbContext;

    public EfChunkEmbeddingStore(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<int> CountPendingAsync(CancellationToken cancellationToken = default) =>
        _dbContext.DocumentChunks
            .Where(c => c.Embedding == null)
            .CountAsync(cancellationToken);

    public async IAsyncEnumerable<IReadOnlyList<PendingChunk>> GetPendingBatchesAsync(
        int batchSize,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Keyset pagination by Id. Because embedded rows drop out of the filter,
        // advancing by last-seen Id avoids skipping rows across pages.
        Guid? lastId = null;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var query = _dbContext.DocumentChunks
                .AsNoTracking()
                .Where(c => c.Embedding == null);

            if (lastId is not null)
            {
                query = query.Where(c => c.Id.CompareTo(lastId.Value) > 0);
            }

            var page = await query
                .OrderBy(c => c.Id)
                .Take(batchSize)
                .Select(c => new PendingChunk(c.Id, c.Content))
                .ToListAsync(cancellationToken);

            if (page.Count == 0)
            {
                yield break;
            }

            lastId = page[^1].Id;
            yield return page;
        }
    }

    public async Task<int> SaveEmbeddingsAsync(
        IReadOnlyDictionary<Guid, float[]> embeddings,
        CancellationToken cancellationToken = default)
    {
        if (embeddings.Count == 0)
        {
            return 0;
        }

        // Parameterized SQL with explicit Vector parameters: EF's change-tracking
        // save path cannot infer the pgvector parameter type for the float[]->Vector
        // value converter, while raw parameters are mapped correctly by the
        // Pgvector.EntityFrameworkCore plugin (same as PgVectorSearchService).
        // "AND embedding IS NULL" keeps the backfill idempotent.
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var saved = 0;
        foreach (var (id, data) in embeddings)
        {
            var vector = new Vector(data);
            saved += await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE document_chunks
                SET "Embedding" = {vector}
                WHERE "Id" = {id} AND "Embedding" IS NULL
                """,
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return saved;
    }
}
