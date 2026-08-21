using System.Runtime.CompilerServices;
using LiaraAI.Application.Documentation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LiaraAI.Infrastructure.Documentation;

/// <summary>
/// Reads documentation files from the local filesystem only. Never performs any
/// network access. The root directory comes from configuration
/// (Documentation:SourcePath) and is resolved relative to the application base
/// directory when it is not absolute.
/// </summary>
public sealed class FileSystemDocumentFileSource : IDocumentFileSource
{
    // Directories that never contain publishable documentation content.
    private static readonly HashSet<string> IgnoredDirectories =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".git", "node_modules", "bin", "obj", "build", "dist",
            "output", "generated", ".next", ".turbo", ".cache", "tmp", "temp"
        };

    private static readonly string[] MarkdownExtensions = { ".md", ".mdx" };

    private readonly DocumentationOptions _options;
    private readonly ILogger<FileSystemDocumentFileSource> _logger;

    public FileSystemDocumentFileSource(
        IOptions<DocumentationOptions> options,
        ILogger<FileSystemDocumentFileSource> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async IAsyncEnumerable<DocumentationFile> DiscoverAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var root = ResolveRoot();

        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException(
                $"Documentation source path '{root}' does not exist. " +
                $"Set '{DocumentationOptions.SectionName}:SourcePath' to the local docs directory.");
        }

        _logger.LogInformation("Discovering documentation files under {Root}", root);

        foreach (var path in EnumerateMarkdownFiles(root))
        {
            cancellationToken.ThrowIfCancellationRequested();

            string content;
            try
            {
                content = await File.ReadAllTextAsync(path, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Could not read documentation file {Path}", path);
                continue;
            }

            var relative = System.IO.Path.GetRelativePath(root, path)
                .Replace('\\', '/');

            yield return new DocumentationFile(relative, content);
        }
    }

    private string ResolveRoot()
    {
        var configured = _options.SourcePath;
        if (System.IO.Path.IsPathRooted(configured))
        {
            return System.IO.Path.GetFullPath(configured);
        }

        // Relative paths are resolved against the process working directory,
        // which is the startup project directory when running `dotnet run`.
        return System.IO.Path.GetFullPath(
            System.IO.Path.Combine(Directory.GetCurrentDirectory(), configured));
    }

    /// <summary>
    /// Depth-first enumeration that skips ignored directories. Implemented
    /// manually (rather than Directory.EnumerateFiles with AllDirectories) so
    /// that large vendor/generated trees are never descended into.
    /// </summary>
    private static IEnumerable<string> EnumerateMarkdownFiles(string root)
    {
        var stack = new Stack<string>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var dir = stack.Pop();

            string[] subDirs;
            try
            {
                subDirs = Directory.GetDirectories(dir);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or DirectoryNotFoundException)
            {
                continue;
            }

            foreach (var sub in subDirs)
            {
                var name = System.IO.Path.GetFileName(sub);
                if (!IgnoredDirectories.Contains(name))
                {
                    stack.Push(sub);
                }
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(dir);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or DirectoryNotFoundException)
            {
                continue;
            }

            foreach (var file in files)
            {
                var ext = System.IO.Path.GetExtension(file);
                if (MarkdownExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
                {
                    yield return file;
                }
            }
        }
    }
}
