namespace LiaraAI.Application.Documentation.Chunking;

/// <summary>A produced chunk with its heading context.</summary>
public sealed record DocumentChunkDraft(
    int ChunkIndex,
    string? Heading,
    string? HeadingPath,
    string Content)
{
    public int CharacterCount => Content.Length;
}
