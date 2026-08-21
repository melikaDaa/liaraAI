namespace LiaraAI.Application.Documentation;

/// <summary>
/// Options controlling documentation ingestion. Bound from the "Documentation"
/// configuration section. No filesystem path is hardcoded in code.
/// </summary>
public sealed class DocumentationOptions
{
    public const string SectionName = "Documentation";

    /// <summary>
    /// Root directory (absolute or relative to the app content root) that
    /// contains the local documentation files. Required.
    /// </summary>
    public string SourcePath { get; set; } = string.Empty;

    public ChunkingOptions Chunking { get; set; } = new();

    public sealed class ChunkingOptions
    {
        /// <summary>Preferred maximum chunk size in characters.</summary>
        public int MaxCharacters { get; set; } = 4000;

        /// <summary>Chunks smaller than this are merged with a neighbour when possible.</summary>
        public int MinCharacters { get; set; } = 300;
    }
}
