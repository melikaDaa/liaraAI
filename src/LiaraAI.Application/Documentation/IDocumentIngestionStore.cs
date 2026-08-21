using LiaraAI.Domain.Documents;

namespace LiaraAI.Application.Documentation;

/// <summary>
/// Persists ingested documents. Kept as a focused contract (not a generic
/// repository) because ingestion is the only current use case.
/// </summary>
public interface IDocumentIngestionStore
{
    /// <summary>
    /// Upsert a document (and its chunks) identified by its relative path.
    /// Existing chunks for the document are replaced.
    /// </summary>
    Task UpsertAsync(Document document, CancellationToken cancellationToken = default);
}
