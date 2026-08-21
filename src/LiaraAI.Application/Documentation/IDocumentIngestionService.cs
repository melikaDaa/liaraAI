namespace LiaraAI.Application.Documentation;

/// <summary>Summary of an ingestion run.</summary>
public sealed record IngestionResult(
    int FilesDiscovered,
    int DocumentsIngested,
    int ChunksCreated,
    int FilesFailed);

/// <summary>
/// Orchestrates the local documentation ingestion pipeline:
/// discovery → parsing → metadata/heading extraction → chunking → persistence.
/// </summary>
public interface IDocumentIngestionService
{
    Task<IngestionResult> IngestAsync(CancellationToken cancellationToken = default);
}
