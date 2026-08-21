using System.Text;
using System.Text.RegularExpressions;
using LiaraAI.Application.Documentation.Parsing;

namespace LiaraAI.Application.Documentation.Chunking;

/// <summary>
/// Heading-aware, code-block-safe chunker.
///
/// Strategy (in order of preference):
///   1. Treat each heading-delimited section as the natural unit.
///   2. Sections that fit within MaxCharacters become a single chunk.
///   3. Oversized sections are split on paragraph boundaries (blank lines),
///      never inside a fenced code block.
///   4. A single paragraph/code block larger than MaxCharacters is split at
///      line boundaries, and only as a last resort at raw character positions.
///   5. Tiny trailing fragments below MinCharacters are merged into the previous
///      chunk of the same section to avoid meaningless chunks.
///
/// Every chunk is prefixed with its heading so retrieval keeps context.
/// </summary>
public sealed class HeadingAwareChunker
{
    private static readonly Regex FenceRegex =
        new(@"^\s*(`{3,}|~{3,})", RegexOptions.Compiled);

    private readonly int _maxCharacters;
    private readonly int _minCharacters;

    public HeadingAwareChunker(int maxCharacters, int minCharacters)
    {
        if (maxCharacters <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCharacters));
        }

        // MinCharacters must not exceed MaxCharacters.
        _maxCharacters = maxCharacters;
        _minCharacters = Math.Clamp(minCharacters, 0, maxCharacters);
    }

    public IReadOnlyList<DocumentChunkDraft> Chunk(ParsedDocument document)
    {
        var drafts = new List<(string? Heading, string? HeadingPath, string Content)>();

        foreach (var section in document.Sections)
        {
            var headingLine = section.Heading is null
                ? null
                : new string('#', section.Heading.Level) + " " + section.Heading.Text;

            var body = section.Body.Trim();

            // Compose the section text (heading + body). Skip fully empty sections.
            var sectionText = Combine(headingLine, body);
            if (sectionText.Length == 0)
            {
                continue;
            }

            IReadOnlyList<string> parts = sectionText.Length <= _maxCharacters
                ? new[] { sectionText }
                : SplitOversized(headingLine, body);

            foreach (var part in parts)
            {
                if (part.Trim().Length == 0)
                {
                    continue;
                }

                drafts.Add((section.Heading?.Text,
                    string.IsNullOrEmpty(section.HeadingPath) ? null : section.HeadingPath,
                    part));
            }
        }

        MergeTinyChunks(drafts);

        var result = new List<DocumentChunkDraft>(drafts.Count);
        for (var i = 0; i < drafts.Count; i++)
        {
            result.Add(new DocumentChunkDraft(i, drafts[i].Heading, drafts[i].HeadingPath, drafts[i].Content.Trim()));
        }

        return result;
    }

    /// <summary>
    /// Split an oversized section into pieces on paragraph boundaries while
    /// keeping fenced code blocks intact. Each emitted piece is re-prefixed with
    /// the heading so context is preserved across the split.
    /// </summary>
    private IReadOnlyList<string> SplitOversized(string? headingLine, string body)
    {
        var blocks = SplitIntoBlocks(body);
        var pieces = new List<string>();
        var current = new StringBuilder();

        void FlushCurrent()
        {
            if (current.Length > 0)
            {
                pieces.Add(current.ToString().TrimEnd());
                current.Clear();
            }
        }

        var headingPrefix = headingLine is null ? string.Empty : headingLine + "\n\n";

        foreach (var block in blocks)
        {
            var prospectiveLength = headingPrefix.Length + current.Length + block.Length + 2;

            if (current.Length > 0 && prospectiveLength > _maxCharacters)
            {
                FlushCurrent();
            }

            if (headingPrefix.Length + block.Length > _maxCharacters)
            {
                // A single block still too large.
                FlushCurrent();

                if (IsFencedCodeBlock(block))
                {
                    // Never split a fenced code block: keep it intact even when
                    // it exceeds MaxCharacters, so the fences stay balanced.
                    pieces.Add(block);
                }
                else
                {
                    foreach (var piece in HardSplit(block))
                    {
                        pieces.Add(piece);
                    }
                }
                continue;
            }

            if (current.Length > 0)
            {
                current.Append("\n\n");
            }
            current.Append(block);
        }

        FlushCurrent();

        // Re-attach the heading to every piece for context.
        return pieces
            .Select(p => Combine(headingLine, p))
            .ToList();
    }

    /// <summary>
    /// Split body into blocks separated by blank lines, treating a fenced code
    /// block as a single indivisible block.
    /// </summary>
    private static List<string> SplitIntoBlocks(string body)
    {
        var blocks = new List<string>();
        var buffer = new StringBuilder();
        var inFence = false;
        string? fenceMarker = null;

        void Flush()
        {
            var text = buffer.ToString().Trim();
            if (text.Length > 0)
            {
                blocks.Add(text);
            }
            buffer.Clear();
        }

        foreach (var line in body.Split('\n'))
        {
            var fence = FenceRegex.Match(line);
            if (fence.Success)
            {
                var marker = fence.Groups[1].Value[..1];
                if (!inFence)
                {
                    inFence = true;
                    fenceMarker = marker;
                }
                else if (fenceMarker == marker)
                {
                    inFence = false;
                    fenceMarker = null;
                    buffer.Append(line).Append('\n');
                    Flush();
                    continue;
                }

                buffer.Append(line).Append('\n');
                continue;
            }

            if (!inFence && line.Trim().Length == 0)
            {
                Flush();
            }
            else
            {
                buffer.Append(line).Append('\n');
            }
        }

        // A dangling unterminated fence is still emitted as one block.
        Flush();
        return blocks;
    }

    private static bool IsFencedCodeBlock(string block)
    {
        var trimmed = block.TrimStart();
        return FenceRegex.IsMatch(trimmed);
    }

    /// <summary>Last-resort splitting: by lines, then by raw characters.</summary>
    private IReadOnlyList<string> HardSplit(string block)
    {
        var pieces = new List<string>();
        var current = new StringBuilder();

        foreach (var line in block.Split('\n'))
        {
            if (current.Length > 0 && current.Length + line.Length + 1 > _maxCharacters)
            {
                pieces.Add(current.ToString().TrimEnd());
                current.Clear();
            }

            if (line.Length > _maxCharacters)
            {
                if (current.Length > 0)
                {
                    pieces.Add(current.ToString().TrimEnd());
                    current.Clear();
                }

                for (var i = 0; i < line.Length; i += _maxCharacters)
                {
                    pieces.Add(line.Substring(i, Math.Min(_maxCharacters, line.Length - i)));
                }
                continue;
            }

            if (current.Length > 0)
            {
                current.Append('\n');
            }
            current.Append(line);
        }

        if (current.Length > 0)
        {
            pieces.Add(current.ToString().TrimEnd());
        }

        return pieces;
    }

    /// <summary>
    /// Merge consecutive chunks that share the same heading path when a chunk is
    /// below MinCharacters and merging keeps the result within MaxCharacters.
    /// </summary>
    private void MergeTinyChunks(List<(string? Heading, string? HeadingPath, string Content)> drafts)
    {
        if (_minCharacters <= 0)
        {
            return;
        }

        for (var i = drafts.Count - 1; i > 0; i--)
        {
            var currentLen = drafts[i].Content.Trim().Length;
            if (currentLen >= _minCharacters)
            {
                continue;
            }

            var prev = drafts[i - 1];
            if (prev.HeadingPath != drafts[i].HeadingPath)
            {
                continue;
            }

            var merged = prev.Content.TrimEnd() + "\n\n" + drafts[i].Content.TrimStart();
            if (merged.Length > _maxCharacters)
            {
                continue;
            }

            drafts[i - 1] = (prev.Heading, prev.HeadingPath, merged);
            drafts.RemoveAt(i);
        }
    }

    private static string Combine(string? headingLine, string body)
    {
        if (string.IsNullOrEmpty(headingLine))
        {
            return body.Trim();
        }

        return body.Trim().Length == 0
            ? headingLine
            : headingLine + "\n\n" + body.Trim();
    }
}
