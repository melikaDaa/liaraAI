using LiaraAI.Application.Documentation.Chunking;
using LiaraAI.Application.Documentation.Parsing;
using LiaraAI.Domain.Documents;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LiaraAI.Application.Documentation;

/// <summary>
/// Orchestrates local documentation ingestion:
/// discovery → parse → metadata/heading extraction → heading-aware chunking → persist.
/// Embeddings are intentionally left null in this milestone.
/// </summary>
public sealed class DocumentIngestionService : IDocumentIngestionService
{
    private readonly IDocumentFileSource _fileSource;
    private readonly IDocumentIngestionStore _store;
    private readonly MarkdownParser _parser;
    private readonly DocumentationOptions _options;
    private readonly ILogger<DocumentIngestionService> _logger;

    public DocumentIngestionService(
        IDocumentFileSource fileSource,
        IDocumentIngestionStore store,
        MarkdownParser parser,
        IOptions<DocumentationOptions> options,
        ILogger<DocumentIngestionService> logger)
    {
        _fileSource = fileSource;
        _store = store;
        _parser = parser;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IngestionResult> IngestAsync(CancellationToken cancellationToken = default)
    {
        var chunker = new HeadingAwareChunker(
            _options.Chunking.MaxCharacters,
            _options.Chunking.MinCharacters);

        var discovered = 0;
        var ingested = 0;
        var chunkCount = 0;
        var failed = 0;

        await foreach (var file in _fileSource.DiscoverAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            discovered++;

            try
            {
                var parsed = _parser.Parse(file.RelativePath, file.Content);
                var drafts = chunker.Chunk(parsed);

                var document = new Document
                {
                    Title = parsed.Title,
                    Url = parsed.Url,
                    Path = file.RelativePath,
                    Category = parsed.Category,
                    Content = parsed.Content,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    Chunks = drafts.Select(d => new DocumentChunk
                    {
                        ChunkIndex = d.ChunkIndex,
                        Heading = d.Heading,
                        HeadingPath = d.HeadingPath,
                        Content = d.Content,
                        CharacterCount = d.CharacterCount,
                        Embedding = null // computed in a later milestone
                    }).ToList()
                };

                await _store.UpsertAsync(document, cancellationToken);

                ingested++;
                chunkCount += drafts.Count;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // One malformed file must not stop the whole ingestion run.
                failed++;
                _logger.LogWarning(ex, "Failed to ingest documentation file {Path}", file.RelativePath);
            }
        }

        _logger.LogInformation(
            "Documentation ingestion complete. Discovered={Discovered} Ingested={Ingested} Chunks={Chunks} Failed={Failed}",
            discovered, ingested, chunkCount, failed);

        return new IngestionResult(discovered, ingested, chunkCount, failed);
    }
}
