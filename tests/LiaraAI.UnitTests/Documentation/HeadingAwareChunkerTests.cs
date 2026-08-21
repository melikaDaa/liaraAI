using LiaraAI.Application.Documentation.Chunking;
using LiaraAI.Application.Documentation.Parsing;

namespace LiaraAI.UnitTests.Documentation;

public class HeadingAwareChunkerTests
{
    private readonly MarkdownParser _parser = new();

    private static string Repeat(string s, int times) => string.Concat(Enumerable.Repeat(s, times));

    [Fact]
    public void Small_sections_stay_within_max_and_carry_heading()
    {
        var raw = "# Title\n\nShort intro paragraph.\n\n## Section A\n\nSome content in A.";
        var parsed = _parser.Parse("x/foo.md", raw);

        var chunker = new HeadingAwareChunker(maxCharacters: 4000, minCharacters: 0);
        var chunks = chunker.Chunk(parsed);

        Assert.NotEmpty(chunks);
        Assert.All(chunks, c => Assert.True(c.CharacterCount <= 4000));
        Assert.Contains(chunks, c => c.HeadingPath == "Title > Section A");
    }

    [Fact]
    public void Oversized_section_splits_on_paragraph_boundaries()
    {
        var para = Repeat("word ", 100).Trim();       // ~500 chars
        var body = string.Join("\n\n", Enumerable.Repeat(para, 10)); // ~5000 chars
        var raw = $"# Big\n\n{body}";
        var parsed = _parser.Parse("x/big.md", raw);

        var chunker = new HeadingAwareChunker(maxCharacters: 1200, minCharacters: 0);
        var chunks = chunker.Chunk(parsed);

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, c => Assert.True(c.CharacterCount <= 1200, $"chunk was {c.CharacterCount}"));
        // Heading context is repeated on every piece.
        Assert.All(chunks, c => Assert.StartsWith("# Big", c.Content));
    }

    [Fact]
    public void Code_block_is_not_split_across_chunks()
    {
        var codeLines = string.Join('\n', Enumerable.Range(0, 60).Select(i => $"line_{i} = {i}"));
        var raw = $"# Code\n\nBefore.\n\n```python\n{codeLines}\n```\n\nAfter.";
        var parsed = _parser.Parse("x/code.md", raw);

        var chunker = new HeadingAwareChunker(maxCharacters: 400, minCharacters: 0);
        var chunks = chunker.Chunk(parsed);

        // The fenced block must live entirely inside a single chunk with balanced fences.
        var fenceChunk = chunks.Single(c => c.Content.Contains("```python"));
        var fenceCount = fenceChunk.Content.Split("```").Length - 1;
        Assert.Equal(2, fenceCount);
        Assert.Contains("line_0 = 0", fenceChunk.Content);
        Assert.Contains("line_59 = 59", fenceChunk.Content);
    }

    [Fact]
    public void Tiny_chunks_are_merged_within_same_heading_path()
    {
        var raw = "# T\n\nabc\n\ndef\n\nghi";
        var parsed = _parser.Parse("x/tiny.md", raw);

        var withMerge = new HeadingAwareChunker(maxCharacters: 4000, minCharacters: 300).Chunk(parsed);

        // All tiny fragments collapse into a single chunk for the section.
        Assert.Single(withMerge);
    }

    [Fact]
    public void Produces_no_empty_chunks()
    {
        var raw = "# A\n\n\n\n## B\n\n\n\n## C\n\ncontent";
        var parsed = _parser.Parse("x/foo.md", raw);

        var chunks = new HeadingAwareChunker(4000, 0).Chunk(parsed);

        Assert.All(chunks, c => Assert.False(string.IsNullOrWhiteSpace(c.Content)));
    }
}
