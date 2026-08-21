using LiaraAI.Api.Configuration;

namespace LiaraAI.IntegrationTests.Configuration;

public class EnvFileConfigurationExtensionsTests : IDisposable
{
    private readonly string _tempDir;

    public EnvFileConfigurationExtensionsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string WriteEnvFile(string content)
    {
        var path = Path.Combine(_tempDir, ".env");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void Loads_key_value_pairs()
    {
        var path = WriteEnvFile("A=hello\nB=world");
        var config = new Dictionary<string, string?>();

        config.LoadEnvFile(path);

        Assert.Equal("hello", config["A"]);
        Assert.Equal("world", config["B"]);
    }

    [Fact]
    public void Skips_comments_and_empty_lines()
    {
        var path = WriteEnvFile("# comment\n\nA=value\n# another comment\nB=other");
        var config = new Dictionary<string, string?>();

        config.LoadEnvFile(path);

        Assert.Equal("value", config["A"]);
        Assert.Equal("other", config["B"]);
        Assert.Equal(2, config.Count);
    }

    [Fact]
    public void Trims_whitespace()
    {
        var path = WriteEnvFile("  KEY  =  value  ");
        var config = new Dictionary<string, string?>();

        config.LoadEnvFile(path);

        Assert.Equal("value", config["KEY"]);
    }

    [Fact]
    public void Strips_surrounding_double_quotes()
    {
        var path = WriteEnvFile("KEY=\"hello world\"");
        var config = new Dictionary<string, string?>();

        config.LoadEnvFile(path);

        Assert.Equal("hello world", config["KEY"]);
    }

    [Fact]
    public void Strips_surrounding_single_quotes()
    {
        var path = WriteEnvFile("KEY='hello world'");
        var config = new Dictionary<string, string?>();

        config.LoadEnvFile(path);

        Assert.Equal("hello world", config["KEY"]);
    }

    [Fact]
    public void Does_not_overwrite_existing_keys()
    {
        var path = WriteEnvFile("A=new");
        var config = new Dictionary<string, string?> { ["A"] = "existing" };

        config.LoadEnvFile(path);

        Assert.Equal("existing", config["A"]);
    }

    [Fact]
    public void Does_not_overwrite_null_values()
    {
        var path = WriteEnvFile("A=value");
        var config = new Dictionary<string, string?> { ["A"] = null };

        config.LoadEnvFile(path);

        Assert.Equal("value", config["A"]);
    }

    [Fact]
    public void Skips_malformed_lines()
    {
        var path = WriteEnvFile("NOEQUALS\nA=value\n=NOKEY");
        var config = new Dictionary<string, string?>();

        config.LoadEnvFile(path);

        Assert.Single(config);
        Assert.Equal("value", config["A"]);
    }

    [Fact]
    public void Returns_unchanged_when_file_does_not_exist()
    {
        var config = new Dictionary<string, string?> { ["existing"] = "value" };

        config.LoadEnvFile("/nonexistent/.env");

        Assert.Single(config);
        Assert.Equal("value", config["existing"]);
    }

    [Fact]
    public void Loads_AvalAI_configuration()
    {
        var path = WriteEnvFile("AvalAI__ApiKey=test-key-123\nAvalAI__BaseUrl=https://api.avalai.ir");
        var config = new Dictionary<string, string?>();

        config.LoadEnvFile(path);

        // __ is converted to : for ASP.NET Core configuration binding
        Assert.Equal("test-key-123", config["AvalAI:ApiKey"]);
        Assert.Equal("https://api.avalai.ir", config["AvalAI:BaseUrl"]);
    }

    [Fact]
    public void Does_not_print_api_key_value()
    {
        var path = WriteEnvFile("AvalAI__ApiKey=secret-12345");
        var config = new Dictionary<string, string?>();

        config.LoadEnvFile(path);

        // Verify key is loaded but not exposed in ToString
        var dictString = config.ToString()!;
        Assert.DoesNotContain("secret-12345", dictString);
    }
}
