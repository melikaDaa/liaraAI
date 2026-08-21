namespace LiaraAI.Application.Search;

/// <summary>
/// A single semantic search result from the vector store.
/// </summary>
public sealed class SearchResult
{
    public Guid ChunkId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? Heading { get; set; }
    public string? HeadingPath { get; set; }
    public double Similarity { get; set; }
    public Guid DocumentId { get; set; }
    public string DocumentTitle { get; set; } = string.Empty;
    public string? DocumentUrl { get; set; }
}
