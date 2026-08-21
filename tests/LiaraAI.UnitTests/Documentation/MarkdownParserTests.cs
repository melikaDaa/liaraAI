using LiaraAI.Application.Documentation.Parsing;

namespace LiaraAI.UnitTests.Documentation;

public class MarkdownParserTests
{
    private readonly MarkdownParser _parser = new();

    [Fact]
    public void Extracts_original_link_and_strips_it_from_content()
    {
        var raw = "Original link: https://docs.liara.ir/ai/about/\n\n# Title\n\nBody text.";

        var parsed = _parser.Parse("ai/about.md", raw);

        Assert.Equal("https://docs.liara.ir/ai/about/", parsed.Url);
        Assert.DoesNotContain("Original link", parsed.Content);
        Assert.Equal("Title", parsed.Title);
    }

    [Fact]
    public void Title_falls_back_to_first_h1_then_filename()
    {
        var fromH1 = _parser.Parse("x/foo.md", "# Heading One\n\ntext");
        Assert.Equal("Heading One", fromH1.Title);

        var fromFilename = _parser.Parse("x/deploy-with-docker.md", "no heading here");
        Assert.Equal("deploy with docker", fromFilename.Title);
    }

    [Fact]
    public void Frontmatter_title_takes_priority()
    {
        var raw = "---\ntitle: FM Title\ncategory: paas\n---\n\n# H1 Title\n\nbody";

        var parsed = _parser.Parse("paas/x.md", raw);

        Assert.Equal("FM Title", parsed.Title);
        Assert.Equal("paas", parsed.Category);
    }

    [Fact]
    public void Category_derived_from_first_path_segment_when_no_frontmatter()
    {
        var parsed = _parser.Parse("dbaas/redis/how-tos/backup.md", "# X\n\nbody");

        Assert.Equal("dbaas", parsed.Category);
    }

    [Fact]
    public void Url_is_null_when_not_present_locally()
    {
        var parsed = _parser.Parse("x/foo.md", "# Title\n\nno link");

        Assert.Null(parsed.Url);
    }

    [Fact]
    public void Malformed_frontmatter_does_not_throw()
    {
        var raw = "---\nthis is : : not : valid yaml maybe\n---\n\n# Title\n\nbody";

        var parsed = _parser.Parse("x/foo.md", raw);

        Assert.Equal("Title", parsed.Title);
    }

    [Fact]
    public void Builds_deterministic_heading_paths()
    {
        var raw = string.Join('\n', new[]
        {
            "# Docker",
            "intro",
            "## Deployment",
            "d",
            "### Environment Variables",
            "e",
            "### Volumes",
            "v",
            "## Networking",
            "n"
        });

        var parsed = _parser.Parse("x/docker.md", raw);
        var paths = parsed.Sections
            .Where(s => s.Heading is not null)
            .Select(s => s.HeadingPath)
            .ToList();

        Assert.Contains("Docker", paths);
        Assert.Contains("Docker > Deployment", paths);
        Assert.Contains("Docker > Deployment > Environment Variables", paths);
        Assert.Contains("Docker > Deployment > Volumes", paths);
        Assert.Contains("Docker > Networking", paths);
    }

    [Fact]
    public void Hash_inside_code_block_is_not_a_heading()
    {
        var raw = string.Join('\n', new[]
        {
            "# Real Heading",
            "```bash",
            "# this is a shell comment, not a heading",
            "docker compose up -d",
            "```",
            "after"
        });

        var parsed = _parser.Parse("x/foo.md", raw);
        var headingTexts = parsed.Sections
            .Where(s => s.Heading is not null)
            .Select(s => s.Heading!.Text)
            .ToList();

        Assert.Single(headingTexts);
        Assert.Equal("Real Heading", headingTexts[0]);
    }
}
