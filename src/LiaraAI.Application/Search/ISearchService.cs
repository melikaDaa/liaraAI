namespace LiaraAI.Application.Search;

/// <summary>
/// Provider-agnostic semantic search over embedded document chunks.
/// Implemented in Infrastructure using pgvector cosine distance.
/// </summary>
public interface ISearchService
{
    /// <summary>
    /// Find the most relevant chunks for a query text.
    /// </summary>
    Task<IReadOnlyList<SearchResult>> SearchAsync(
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default);
}
