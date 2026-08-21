using System.Text;
using LiaraAI.Application.Search;

namespace LiaraAI.Application.Rag;

/// <summary>
/// Builds structured documentation context for the LLM from search results.
/// Format per chunk:
/// [Source N]
/// Title: ...
/// Heading: ...
/// HeadingPath: ...
/// URL: ...
/// Content:
/// ...
/// </summary>
public sealed class DocumentationContextBuilder : IContextBuilder
{
    public string Build(IReadOnlyList<SearchResult> results, int maxCharacters)
    {
        if (results.Count == 0 || maxCharacters <= 0)
            return string.Empty;

        var sb = new StringBuilder();
        int sourceIndex = 0;
        var seenChunkIds = new HashSet<Guid>();

        foreach (var result in results)
        {
            if (seenChunkIds.Contains(result.ChunkId))
                continue;
            seenChunkIds.Add(result.ChunkId);

            sourceIndex++;

            var entry = FormatSourceEntry(result, sourceIndex);

            if (sb.Length + entry.Length > maxCharacters && sb.Length > 0)
                break;

            if (sb.Length > 0)
                sb.AppendLine().AppendLine("---").AppendLine();

            sb.Append(entry);
        }

        return sb.ToString().TrimEnd();
    }

    private static string FormatSourceEntry(SearchResult result, int index)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[Source {index}]");
        sb.AppendLine($"Title: {result.DocumentTitle}");

        if (!string.IsNullOrEmpty(result.Heading))
            sb.AppendLine($"Heading: {result.Heading}");

        if (!string.IsNullOrEmpty(result.HeadingPath))
            sb.AppendLine($"HeadingPath: {result.HeadingPath}");

        sb.AppendLine($"URL: {result.DocumentUrl ?? "N/A"}");
        sb.AppendLine("Content:");
        sb.Append(result.Content);

        return sb.ToString();
    }
}
