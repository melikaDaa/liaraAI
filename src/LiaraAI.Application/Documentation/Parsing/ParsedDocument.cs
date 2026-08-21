namespace LiaraAI.Application.Documentation.Parsing;

/// <summary>A markdown heading with its level (1-6) and text.</summary>
public sealed record MarkdownHeading(int Level, string Text);

/// <summary>
/// A logical section of a parsed markdown document: a heading plus the body
/// text that follows it (until the next heading of equal-or-higher level),
/// together with the full heading path leading to it.
/// </summary>
public sealed record MarkdownSection(
    MarkdownHeading? Heading,
    string HeadingPath,
    string Body);

/// <summary>The result of parsing a raw markdown documentation file.</summary>
public sealed record ParsedDocument(
    string Title,
    string? Url,
    string? Category,
    string Content,
    IReadOnlyList<MarkdownSection> Sections);
