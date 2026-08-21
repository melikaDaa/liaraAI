namespace LiaraAI.Domain.Documents;

/// <summary>
/// A source documentation page ingested into the RAG pipeline.
/// One <see cref="Document"/> is split into many <see cref="DocumentChunk"/>s.
/// </summary>
public class Document
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Human-readable title of the documentation page.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Canonical URL where the documentation can be viewed. May be null when unknown.</summary>
    public string? Url { get; set; }

    /// <summary>Source path (e.g. repository-relative file path) of the document.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Category / grouping of the document (e.g. "databases", "deployment"). May be null.</summary>
    public string? Category { get; set; }

    /// <summary>Full cleaned textual content of the document.</summary>
    public string Content { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Chunks derived from this document.</summary>
    public List<DocumentChunk> Chunks { get; set; } = new();
}
