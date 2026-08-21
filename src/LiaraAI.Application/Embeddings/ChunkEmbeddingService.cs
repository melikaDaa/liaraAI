using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LiaraAI.Application.Embeddings;

/// <summary>
/// Generates embeddings for all chunks whose embedding is NULL, in batches,
/// with retry and strict validation. Never overwrites existing embeddings.
/// </summary>
public sealed class ChunkEmbeddingService : IChunkEmbeddingService
{
    private readonly IChunkEmbeddingStore _store;
    private readonly IEmbeddingService _embeddingService;
    private readonly EmbeddingProcessingOptions _options;
    private readonly ILogger<ChunkEmbeddingService> _logger;

    public ChunkEmbeddingService(
        IChunkEmbeddingStore store,
        IEmbeddingService embeddingService,
        IOptions<EmbeddingProcessingOptions> options,
        ILogger<ChunkEmbeddingService> logger)
    {
        _store = store;
        _embeddingService = embeddingService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<EmbeddingBackfillResult> BackfillAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var pendingAtStart = await _store.CountPendingAsync(cancellationToken);

        _logger.LogInformation(
            "Embedding backfill started. Chunks requiring embedding={Pending}, BatchSize={BatchSize}",
            pendingAtStart, _options.BatchSize);

        var embedded = 0;
        var failed = 0;
        var batches = 0;
        var batchesFailed = 0;

        await foreach (var batch in _store.GetPendingBatchesAsync(_options.BatchSize, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            batches++;

            _logger.LogInformation("Batch {Batch} started with {Count} chunks", batches, batch.Count);

            IReadOnlyList<float[]> vectors;
            try
            {
                vectors = await EmbedWithRetryAsync(batch.Select(c => c.Content).ToList(), batches, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // A failed batch must not corrupt existing data; skip and continue.
                batchesFailed++;
                failed += batch.Count;
                _logger.LogError(ex, "Batch {Batch} failed after retries; skipping {Count} chunks", batches, batch.Count);
                continue;
            }

            if (vectors.Count != batch.Count)
            {
                batchesFailed++;
                failed += batch.Count;
                _logger.LogError(
                    "Batch {Batch} returned {Returned} vectors for {Expected} inputs; skipping batch",
                    batches, vectors.Count, batch.Count);
                continue;
            }

            var valid = new Dictionary<Guid, float[]>(batch.Count);
            for (var i = 0; i < batch.Count; i++)
            {
                if (EmbeddingValidator.TryValidate(vectors[i], _options.Dimensions, out var error))
                {
                    valid[batch[i].Id] = vectors[i];
                }
                else
                {
                    failed++;
                    _logger.LogWarning("Chunk {ChunkId} produced an invalid embedding: {Error}", batch[i].Id, error);
                }
            }

            if (valid.Count > 0)
            {
                var updated = await _store.SaveEmbeddingsAsync(valid, cancellationToken);
                embedded += updated;
                _logger.LogInformation("Batch {Batch} completed. Embedded={Embedded}", batches, updated);
            }
        }

        stopwatch.Stop();
        _logger.LogInformation(
            "Embedding backfill completed. Embedded={Embedded}, Failed={Failed}, Batches={Batches}, BatchesFailed={BatchesFailed}, DurationMs={Duration}",
            embedded, failed, batches, batchesFailed, stopwatch.ElapsedMilliseconds);

        return new EmbeddingBackfillResult(
            pendingAtStart, embedded, failed, batches, batchesFailed, stopwatch.ElapsedMilliseconds);
    }

    private async Task<IReadOnlyList<float[]>> EmbedWithRetryAsync(
        IReadOnlyList<string> inputs, int batchNumber, CancellationToken cancellationToken)
    {
        var attempt = 0;
        while (true)
        {
            attempt++;
            try
            {
                return await _embeddingService.EmbedAsync(inputs, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (EmbeddingProviderException ex) when (!ex.IsRetryable)
            {
                // Non-retryable provider errors (400, 401, 403) — fail immediately.
                _logger.LogError(
                    "Batch {Batch} failed with non-retryable error: {Error}",
                    batchNumber, ex.Message);
                throw;
            }
            catch (Exception ex) when (attempt <= _options.MaxRetries)
            {
                var delay = TimeSpan.FromMilliseconds(_options.RetryBaseDelayMs * Math.Pow(2, attempt - 1));
                _logger.LogWarning(
                    ex,
                    "Batch {Batch} attempt {Attempt}/{Max} failed; retrying in {Delay}ms",
                    batchNumber, attempt, _options.MaxRetries, delay.TotalMilliseconds);
                await Task.Delay(delay, cancellationToken);
            }
        }
    }
}
