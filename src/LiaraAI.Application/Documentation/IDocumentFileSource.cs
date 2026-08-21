namespace LiaraAI.Application.Documentation;

/// <summary>
/// Discovers and reads local documentation files. Implementations must only
/// read from the local filesystem - never from the network.
/// </summary>
public interface IDocumentFileSource
{
    /// <summary>
    /// Recursively enumerate documentation files under the configured source path.
    /// Throws <see cref="DirectoryNotFoundException"/> if the source path is missing.
    /// </summary>
    IAsyncEnumerable<DocumentationFile> DiscoverAsync(CancellationToken cancellationToken = default);
}
