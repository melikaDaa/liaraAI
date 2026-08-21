namespace LiaraAI.Application.Documentation;

/// <summary>
/// A raw documentation file discovered on the local filesystem.
/// Content is the verbatim file text; RelativePath is relative to the
/// configured documentation source root (never a machine-specific absolute path).
/// </summary>
public sealed record DocumentationFile(string RelativePath, string Content);
