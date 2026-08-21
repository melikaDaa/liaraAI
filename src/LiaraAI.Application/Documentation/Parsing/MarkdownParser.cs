using System.Text;
using System.Text.RegularExpressions;

namespace LiaraAI.Application.Documentation.Parsing;

/// <summary>
/// Parses a raw markdown/mdx documentation file into a <see cref="ParsedDocument"/>.
///
/// Supports two local metadata conventions found in the Liara docs repository:
///   1. YAML-style frontmatter delimited by lines of "---".
///   2. A leading "Original link: &lt;url&gt;" line (used by the exported llms docs).
///
/// All information is derived solely from the file content and its relative
/// path - never from the network.
/// </summary>
public sealed class MarkdownParser
{
    // A markdown ATX heading: 1-6 '#' followed by a space and text.
    private static readonly Regex HeadingRegex =
        new(@"^(#{1,6})\s+(.*?)\s*#*\s*$", RegexOptions.Compiled);

    private static readonly Regex OriginalLinkRegex =
        new(@"^\s*Original link:\s*(?<url>\S+)\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Opening/closing of a fenced code block: ``` or ~~~ (with optional info string).
    private static readonly Regex FenceRegex =
        new(@"^\s*(`{3,}|~{3,})", RegexOptions.Compiled);

    /// <param name="relativePath">Path relative to the source root, using '/' separators.</param>
    /// <param name="rawContent">Verbatim file content.</param>
    public ParsedDocument Parse(string relativePath, string rawContent)
    {
        var content = StripBom(rawContent).Replace("\r\n", "\n").Replace('\r', '\n');

        var frontmatter = ExtractFrontmatter(ref content);

        string? url = frontmatter.GetValueOrDefault("url")
                      ?? frontmatter.GetValueOrDefault("slug");
        url = ExtractOriginalLink(ref content) ?? url;

        var headings = ExtractHeadingsAndFirstH1(content, out var firstH1);

        var title = FirstNonEmpty(
            frontmatter.GetValueOrDefault("title"),
            firstH1,
            FilenameToTitle(relativePath));

        var category = frontmatter.GetValueOrDefault("category")
                       ?? DeriveCategoryFromPath(relativePath);

        var sections = BuildSections(content);

        return new ParsedDocument(
            Title: title,
            Url: string.IsNullOrWhiteSpace(url) ? null : url.Trim(),
            Category: string.IsNullOrWhiteSpace(category) ? null : category,
            Content: content.Trim(),
            Sections: sections);
    }

    private static string StripBom(string s) =>
        s.Length > 0 && s[0] == '\uFEFF' ? s[1..] : s;

    /// <summary>
    /// Removes a leading YAML frontmatter block (delimited by "---") if present,
    /// returning simple key: value pairs. Malformed frontmatter is ignored
    /// (returns empty) and never throws, so one bad file cannot break others.
    /// </summary>
    private static Dictionary<string, string> ExtractFrontmatter(ref string content)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!content.StartsWith("---\n"))
        {
            return result;
        }

        var end = content.IndexOf("\n---", 4, StringComparison.Ordinal);
        if (end < 0)
        {
            return result; // no closing fence -> treat as not frontmatter
        }

        var block = content[4..end];
        try
        {
            foreach (var line in block.Split('\n'))
            {
                var idx = line.IndexOf(':');
                if (idx <= 0)
                {
                    continue;
                }

                var key = line[..idx].Trim();
                var value = line[(idx + 1)..].Trim().Trim('"', '\'');
                if (key.Length > 0 && value.Length > 0)
                {
                    result[key] = value;
                }
            }
        }
        catch
        {
            result.Clear();
        }

        // Advance past the closing delimiter line.
        var after = content.IndexOf('\n', end + 1);
        content = after >= 0 ? content[(after + 1)..] : string.Empty;
        return result;
    }

    private static string? ExtractOriginalLink(ref string content)
    {
        var newlineIdx = content.IndexOf('\n');
        var firstLine = newlineIdx >= 0 ? content[..newlineIdx] : content;

        var match = OriginalLinkRegex.Match(firstLine);
        if (!match.Success)
        {
            return null;
        }

        // Drop the metadata line from the body content.
        content = newlineIdx >= 0 ? content[(newlineIdx + 1)..] : string.Empty;
        content = content.TrimStart('\n');
        return match.Groups["url"].Value;
    }

    /// <summary>
    /// Enumerates headings while skipping any '#' lines inside fenced code blocks,
    /// so shell comments like "# comment" are never treated as headings.
    /// </summary>
    private static List<MarkdownHeading> ExtractHeadingsAndFirstH1(string content, out string? firstH1)
    {
        firstH1 = null;
        var headings = new List<MarkdownHeading>();

        foreach (var (line, insideCode) in EnumerateLines(content))
        {
            if (insideCode)
            {
                continue;
            }

            var m = HeadingRegex.Match(line);
            if (!m.Success)
            {
                continue;
            }

            var level = m.Groups[1].Value.Length;
            var text = m.Groups[2].Value.Trim();
            if (text.Length == 0)
            {
                continue;
            }

            headings.Add(new MarkdownHeading(level, text));
            if (level == 1 && firstH1 is null)
            {
                firstH1 = text;
            }
        }

        return headings;
    }

    /// <summary>
    /// Splits the document into sections at heading boundaries, tracking the full
    /// heading path (e.g. "Docker &gt; Deployment &gt; Volumes"). Content before the
    /// first heading becomes a leading section with an empty heading path.
    /// </summary>
    private static List<MarkdownSection> BuildSections(string content)
    {
        var sections = new List<MarkdownSection>();
        var pathStack = new List<MarkdownHeading>();
        MarkdownHeading? currentHeading = null;
        var currentPath = string.Empty;
        var body = new StringBuilder();

        void Flush()
        {
            var text = body.ToString().Trim();
            if (currentHeading is not null || text.Length > 0)
            {
                sections.Add(new MarkdownSection(currentHeading, currentPath, text));
            }
            body.Clear();
        }

        foreach (var (line, insideCode) in EnumerateLines(content))
        {
            MarkdownHeading? heading = null;
            if (!insideCode)
            {
                var m = HeadingRegex.Match(line);
                if (m.Success)
                {
                    var level = m.Groups[1].Value.Length;
                    var text = m.Groups[2].Value.Trim();
                    if (text.Length > 0)
                    {
                        heading = new MarkdownHeading(level, text);
                    }
                }
            }

            if (heading is not null)
            {
                Flush();

                // Maintain the ancestor stack: pop headings of equal-or-deeper level.
                while (pathStack.Count > 0 && pathStack[^1].Level >= heading.Level)
                {
                    pathStack.RemoveAt(pathStack.Count - 1);
                }
                pathStack.Add(heading);

                currentHeading = heading;
                currentPath = string.Join(" > ", pathStack.Select(h => h.Text));
            }
            else
            {
                body.Append(line).Append('\n');
            }
        }

        Flush();
        return sections;
    }

    /// <summary>
    /// Yields each line together with whether it is inside a fenced code block.
    /// Fence lines themselves are reported as inside-code so headings never match them.
    /// </summary>
    private static IEnumerable<(string Line, bool InsideCode)> EnumerateLines(string content)
    {
        var inFence = false;
        string? fenceMarker = null;

        foreach (var line in content.Split('\n'))
        {
            var fence = FenceRegex.Match(line);
            if (fence.Success)
            {
                var marker = fence.Groups[1].Value[..1]; // '`' or '~'
                if (!inFence)
                {
                    inFence = true;
                    fenceMarker = marker;
                    yield return (line, true);
                    continue;
                }

                if (fenceMarker == marker)
                {
                    inFence = false;
                    fenceMarker = null;
                    yield return (line, true);
                    continue;
                }
            }

            yield return (line, inFence);
        }
    }

    private static string FilenameToTitle(string relativePath)
    {
        var name = relativePath.Replace('\\', '/');
        var slash = name.LastIndexOf('/');
        if (slash >= 0)
        {
            name = name[(slash + 1)..];
        }

        var dot = name.LastIndexOf('.');
        if (dot > 0)
        {
            name = name[..dot];
        }

        name = name.Replace('-', ' ').Replace('_', ' ').Trim();
        return name.Length == 0 ? "Untitled" : name;
    }

    /// <summary>Category = first meaningful path segment (e.g. "paas", "dbaas").</summary>
    private static string? DeriveCategoryFromPath(string relativePath)
    {
        var segments = relativePath.Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length > 1 ? segments[0] : null;
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? "Untitled";
}
