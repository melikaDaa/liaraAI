using LiaraAI.Application.Search;
using LiaraAI.Application.Embeddings;
using LiaraAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pgvector;

namespace LiaraAI.Infrastructure.Search;

/// <summary>
/// pgvector-backed semantic search. Embeds the query text on-the-fly via
/// <see cref="IEmbeddingService"/>, then performs cosine distance search
/// over document chunks with non-null embeddings using raw SQL.
/// </summary>
public sealed class PgVectorSearchService : ISearchService
{
    private readonly AppDbContext _dbContext;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<PgVectorSearchService> _logger;

    public PgVectorSearchService(
        AppDbContext dbContext,
        IEmbeddingService embeddingService,
        ILogger<PgVectorSearchService> logger)
    {
        _dbContext = dbContext;
        _embeddingService = embeddingService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<SearchResult>();
        }

        float[] queryVector;
        try
        {
            var vectors = await _embeddingService.EmbedAsync(
                new[] { query },
                cancellationToken);
            queryVector = vectors[0];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to embed search query.");
            return Array.Empty<SearchResult>();
        }

        var vectorParam = new Vector(queryVector);

        // Table names are lowercase (set by ToTable), column names are PascalCase (default).
        // In PostgreSQL, unquoted identifiers are folded to lowercase, so we must double-quote
        // PascalCase column names to match the actual schema.
        var sql = $@"
            SELECT dc.""Id"" AS ""ChunkId"",
                   dc.""Content"" AS ""Content"",
                   dc.""Heading"" AS ""Heading"",
                   dc.""HeadingPath"" AS ""HeadingPath"",
                   1.0 - (dc.""Embedding"" <=> @p0) AS ""Similarity"",
                   dc.""DocumentId"" AS ""DocumentId"",
                   d.""Title"" AS ""DocumentTitle"",
                   d.""Url"" AS ""DocumentUrl""
            FROM document_chunks dc
            INNER JOIN documents d ON d.""Id"" = dc.""DocumentId""
            WHERE dc.""Embedding"" IS NOT NULL
            ORDER BY dc.""Embedding"" <=> @p0
            LIMIT @p1";

        var results = await _dbContext.Database
            .SqlQueryRaw<SearchResult>(sql, vectorParam, topK)
            .ToListAsync(cancellationToken);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Vector search completed. QueryLength={QueryLength}, TopK={TopK}, Results={ResultCount}",
                query.Length, topK, results.Count);
            for (int i = 0; i < Math.Min(results.Count, 5); i++)
            {
                _logger.LogDebug(
                    "Vector result[{Index}]: Similarity={Similarity:F4}, Title={Title}, Heading={Heading}",
                    i, results[i].Similarity, results[i].DocumentTitle, results[i].Heading);
            }
        }

        return results;
    }
}
