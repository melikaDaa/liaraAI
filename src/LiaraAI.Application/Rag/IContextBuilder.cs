using LiaraAI.Application.Search;

namespace LiaraAI.Application.Rag;

/// <summary>
/// Builds a compact documentation context string from retrieved search results.
/// Responsible for structured formatting, character budget enforcement, and
/// deduplication of source chunks.
/// </summary>
public interface IContextBuilder
{
    /// <summary>
    /// Build a context string from the given search results, respecting
    /// the maximum character budget. Deduplicates by ChunkId.
    /// </summary>
    string Build(IReadOnlyList<SearchResult> results, int maxCharacters);
}
