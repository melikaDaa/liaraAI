namespace LiaraAI.Domain.Documents;

/// <summary>
/// A contiguous chunk of a <see cref="Document"/> used as the unit of retrieval
/// in the RAG pipeline. The embedding vector is populated later by the ingestion
/// pipeline and is therefore nullable at this stage.
/// </summary>
public class DocumentChunk
{
    /// <summary>
    /// Dimension of the embedding vector produced by the initial embedding model
    /// (text-embedding-3-small).
    /// </summary>
    public const int EmbeddingDimensions = 1536;

    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Foreign key to the owning <see cref="Document"/>.</summary>
    public Guid DocumentId { get; set; }

    /// <summary>Navigation to the owning document.</summary>
    public Document? Document { get; set; }

    /// <summary>Textual content of this chunk.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Zero-based position of this chunk within its document.</summary>
    public int ChunkIndex { get; set; }

    /// <summary>Nearest heading this chunk belongs to, if any.</summary>
    public string? Heading { get; set; }

    /// <summary>Full heading hierarchy leading to this chunk (e.g. "Databases &gt; PostgreSQL &gt; Backups").</summary>
    public string? HeadingPath { get; set; }

    /// <summary>Number of characters in <see cref="Content"/>.</summary>
    public int CharacterCount { get; set; }

    /// <summary>
    /// Embedding vector for this chunk. Null until the ingestion pipeline computes it.
    /// Stored as a pgvector column of dimension <see cref="EmbeddingDimensions"/>.
    /// Kept as a plain CLR array so Domain stays free of infrastructure types.
    /// </summary>
    public float[]? Embedding { get; set; }
}
