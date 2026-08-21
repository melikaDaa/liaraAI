using LiaraAI.Application.Embeddings;
using LiaraAI.Domain.Documents;
using LiaraAI.Infrastructure.Embeddings;
using LiaraAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LiaraAI.IntegrationTests.Embeddings;

/// <summary>
/// Store-level idempotency/round-trip tests exercising real pgvector storage.
///
/// These require a local PostgreSQL (pgvector) server. When none is reachable the
/// tests skip cleanly instead of failing, so CI without a database is unaffected.
/// Each test provisions and drops its own isolated database. The real AvalAI API
/// is never called.
/// </summary>
public class EfChunkEmbeddingStoreTests
{
    private static string AdminConnectionString =>
        Environment.GetEnvironmentVariable("TEST_POSTGRES")
        ?? "Host=localhost;Port=5432;Database=postgres;Username=liaraai;Password=localdev";

    private static float[] Vec(float value) => Enumerable.Repeat(value, 1536).ToArray();

    private static bool ServerAvailable()
    {
        try
        {
            using var conn = new NpgsqlConnection(AdminConnectionString);
            conn.Open();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private sealed class TestDatabase : IDisposable
    {
        private readonly string _admin;
        public string Name { get; }
        public string ConnectionString { get; }

        public TestDatabase(string admin)
        {
            _admin = admin;
            Name = "liaraai_test_" + Guid.NewGuid().ToString("N");
            using (var conn = new NpgsqlConnection(admin))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"CREATE DATABASE \"{Name}\"";
                cmd.ExecuteNonQuery();
            }

            var builder = new NpgsqlConnectionStringBuilder(admin) { Database = Name };
            ConnectionString = builder.ConnectionString;
        }

        public AppDbContext NewContext() =>
            new(new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(ConnectionString, o => o.UseVector())
                .Options);

        public void Dispose()
        {
            NpgsqlConnection.ClearAllPools();
            using var conn = new NpgsqlConnection(_admin);
            conn.Open();

            using (var terminate = conn.CreateCommand())
            {
                terminate.CommandText =
                    $"SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname='{Name}'";
                terminate.ExecuteNonQuery();
            }

            using (var drop = conn.CreateCommand())
            {
                drop.CommandText = $"DROP DATABASE IF EXISTS \"{Name}\"";
                drop.ExecuteNonQuery();
            }
        }
    }

    private static async Task<(Guid embedded, Guid pending)> SeedAsync(TestDatabase db)
    {
        await using var ctx = db.NewContext();
        await ctx.Database.EnsureCreatedAsync();

        var doc = new Document { Title = "T", Path = "p.md", Content = "c" };
        var embedded = new DocumentChunk { ChunkIndex = 0, Content = "already", CharacterCount = 7, Embedding = Vec(0.5f) };
        var pending = new DocumentChunk { ChunkIndex = 1, Content = "todo", CharacterCount = 4, Embedding = null };
        doc.Chunks.Add(embedded);
        doc.Chunks.Add(pending);
        ctx.Documents.Add(doc);
        await ctx.SaveChangesAsync();
        return (embedded.Id, pending.Id);
    }

    [SkippableFact]
    public async Task CountPending_only_counts_null_embeddings()
    {
        Skip.IfNot(ServerAvailable(), "PostgreSQL not available.");
        using var db = new TestDatabase(AdminConnectionString);
        await SeedAsync(db);

        await using var ctx = db.NewContext();
        var store = new EfChunkEmbeddingStore(ctx);

        Assert.Equal(1, await store.CountPendingAsync());
    }

    [SkippableFact]
    public async Task GetPendingBatches_returns_only_null_chunks()
    {
        Skip.IfNot(ServerAvailable(), "PostgreSQL not available.");
        using var db = new TestDatabase(AdminConnectionString);
        var (_, pending) = await SeedAsync(db);

        await using var ctx = db.NewContext();
        var store = new EfChunkEmbeddingStore(ctx);

        var ids = new List<Guid>();
        await foreach (var batch in store.GetPendingBatchesAsync(10))
        {
            ids.AddRange(batch.Select(c => c.Id));
        }

        Assert.Single(ids);
        Assert.Equal(pending, ids[0]);
    }

    [SkippableFact]
    public async Task SaveEmbeddings_stores_1536_vector_and_can_be_retrieved()
    {
        Skip.IfNot(ServerAvailable(), "PostgreSQL not available.");
        using var db = new TestDatabase(AdminConnectionString);
        var (_, pending) = await SeedAsync(db);

        await using (var ctx = db.NewContext())
        {
            var store = new EfChunkEmbeddingStore(ctx);
            var updated = await store.SaveEmbeddingsAsync(
                new Dictionary<Guid, float[]> { [pending] = Vec(0.9f) });
            Assert.Equal(1, updated);
        }

        await using (var ctx = db.NewContext())
        {
            var chunk = await ctx.DocumentChunks.FirstAsync(c => c.Id == pending);
            Assert.NotNull(chunk.Embedding);
            Assert.Equal(1536, chunk.Embedding!.Length);
            Assert.Equal(0.9f, chunk.Embedding[0]);
        }
    }

    [SkippableFact]
    public async Task SaveEmbeddings_does_not_overwrite_existing_embedding()
    {
        Skip.IfNot(ServerAvailable(), "PostgreSQL not available.");
        using var db = new TestDatabase(AdminConnectionString);
        var (embedded, _) = await SeedAsync(db);

        await using (var ctx = db.NewContext())
        {
            var store = new EfChunkEmbeddingStore(ctx);
            var updated = await store.SaveEmbeddingsAsync(
                new Dictionary<Guid, float[]> { [embedded] = Vec(0.1f) });
            Assert.Equal(0, updated);
        }

        await using (var ctx = db.NewContext())
        {
            var chunk = await ctx.DocumentChunks.FirstAsync(c => c.Id == embedded);
            Assert.Equal(0.5f, chunk.Embedding![0]); // unchanged
        }
    }
}
