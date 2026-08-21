using LiaraAI.Application.Embeddings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LiaraAI.UnitTests.Embeddings;

public class ChunkEmbeddingServiceTests
{
    private static float[] Vec(float v) => Enumerable.Repeat(v, 1536).ToArray();

    private sealed class FakeStore : IChunkEmbeddingStore
    {
        private readonly List<PendingChunk> _pending;
        public Dictionary<Guid, float[]> Saved { get; } = new();
        public int SaveCalls { get; private set; }

        public FakeStore(IEnumerable<PendingChunk> pending) => _pending = pending.ToList();

        public Task<int> CountPendingAsync(CancellationToken ct = default) => Task.FromResult(_pending.Count);

        public async IAsyncEnumerable<IReadOnlyList<PendingChunk>> GetPendingBatchesAsync(
            int batchSize,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            for (var i = 0; i < _pending.Count; i += batchSize)
            {
                await Task.Yield();
                yield return _pending.Skip(i).Take(batchSize).ToList();
            }
        }

        public Task<int> SaveEmbeddingsAsync(IReadOnlyDictionary<Guid, float[]> embeddings, CancellationToken ct = default)
        {
            SaveCalls++;
            foreach (var kv in embeddings)
            {
                Saved[kv.Key] = kv.Value;
            }
            return Task.FromResult(embeddings.Count);
        }
    }

    private sealed class FakeEmbeddingService : IEmbeddingService
    {
        private readonly Func<IReadOnlyList<string>, IReadOnlyList<float[]>>? _responder;
        private readonly Func<IReadOnlyList<string>, Task<IReadOnlyList<float[]>>>? _asyncResponder;
        public int Calls { get; private set; }

        public FakeEmbeddingService(Func<IReadOnlyList<string>, IReadOnlyList<float[]>> responder)
            => _responder = responder;

        public FakeEmbeddingService(Func<IReadOnlyList<string>, Task<IReadOnlyList<float[]>>> asyncResponder)
            => _asyncResponder = asyncResponder;

        public async Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken ct = default)
        {
            Calls++;
            if (_asyncResponder is not null)
                return await _asyncResponder(inputs);
            return _responder!(inputs);
        }
    }

    private static ChunkEmbeddingService Create(
        IChunkEmbeddingStore store, IEmbeddingService embed, int batchSize = 2, int retries = 2)
    {
        var options = Options.Create(new EmbeddingProcessingOptions
        {
            BatchSize = batchSize,
            MaxRetries = retries,
            RetryBaseDelayMs = 1,
            Dimensions = 1536
        });
        return new ChunkEmbeddingService(store, embed, options, NullLogger<ChunkEmbeddingService>.Instance);
    }

    [Fact]
    public async Task Embeds_all_pending_chunks()
    {
        var pending = new[]
        {
            new PendingChunk(Guid.NewGuid(), "a"),
            new PendingChunk(Guid.NewGuid(), "b"),
            new PendingChunk(Guid.NewGuid(), "c"),
        };
        var store = new FakeStore(pending);
        var embed = new FakeEmbeddingService(inputs => inputs.Select(_ => Vec(0.1f)).ToArray());

        var result = await Create(store, embed).BackfillAsync();

        Assert.Equal(3, result.ChunksEmbedded);
        Assert.Equal(0, result.ChunksFailed);
        Assert.Equal(3, store.Saved.Count);
    }

    [Fact]
    public async Task Nothing_pending_makes_no_provider_calls()
    {
        var store = new FakeStore(Array.Empty<PendingChunk>());
        var embed = new FakeEmbeddingService(inputs => inputs.Select(_ => Vec(0.1f)).ToArray());

        var result = await Create(store, embed).BackfillAsync();

        Assert.Equal(0, result.PendingAtStart);
        Assert.Equal(0, result.ChunksEmbedded);
        Assert.Equal(0, embed.Calls);
    }

    [Fact]
    public async Task Invalid_dimension_vectors_are_not_saved()
    {
        var pending = new[] { new PendingChunk(Guid.NewGuid(), "a") };
        var store = new FakeStore(pending);
        // Wrong dimension -> must be rejected by validation, never persisted.
        var embed = new FakeEmbeddingService(inputs => inputs.Select(_ => Enumerable.Repeat(0.1f, 768).ToArray()).ToArray());

        var result = await Create(store, embed).BackfillAsync();

        Assert.Equal(0, result.ChunksEmbedded);
        Assert.Equal(1, result.ChunksFailed);
        Assert.Empty(store.Saved);
    }

    [Fact]
    public async Task Retries_then_succeeds_on_transient_failure()
    {
        var pending = new[] { new PendingChunk(Guid.NewGuid(), "a") };
        var store = new FakeStore(pending);

        var attempts = 0;
        var embed = new FakeEmbeddingService(inputs =>
        {
            attempts++;
            if (attempts == 1)
            {
                throw new HttpRequestException("transient");
            }
            return inputs.Select(_ => Vec(0.2f)).ToArray();
        });

        var result = await Create(store, embed).BackfillAsync();

        Assert.Equal(1, result.ChunksEmbedded);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task Failed_batch_after_retries_is_skipped_without_saving()
    {
        var pending = new[] { new PendingChunk(Guid.NewGuid(), "a") };
        var store = new FakeStore(pending);
        var embed = new FakeEmbeddingService(async inputs =>
        {
            await Task.CompletedTask;
            throw new HttpRequestException("always fails");
        });

        var result = await Create(store, embed, retries: 1).BackfillAsync();

        Assert.Equal(0, result.ChunksEmbedded);
        Assert.Equal(1, result.BatchesFailed);
        Assert.Equal(0, store.SaveCalls);
    }

    [Fact]
    public async Task Non_retryable_error_is_not_retried()
    {
        var pending = new[] { new PendingChunk(Guid.NewGuid(), "a") };
        var store = new FakeStore(pending);

        var attempts = 0;
        var embed = new FakeEmbeddingService(async inputs =>
        {
            attempts++;
            // Simulate 401 Unauthorized — non-retryable.
            await Task.CompletedTask;
            throw new EmbeddingProviderException("Unauthorized", 401);
        });

        var result = await Create(store, embed, retries: 3).BackfillAsync();

        Assert.Equal(0, result.ChunksEmbedded);
        Assert.Equal(1, result.ChunksFailed);
        Assert.Equal(1, attempts); // Only one attempt, no retries.
        Assert.Equal(0, store.SaveCalls);
    }
}
