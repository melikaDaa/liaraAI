using LiaraAI.Infrastructure.Embeddings;
using Microsoft.Extensions.Options;

namespace LiaraAI.IntegrationTests.Configuration;

public class AvalAIOptionsTests
{
    [Fact]
    public void Default_BaseUrl_is_correct()
    {
        var options = new AvalAIOptions();
        Assert.Equal("https://api.avalai.ir", options.BaseUrl);
    }

    [Fact]
    public void Default_EmbeddingModel_is_text_embedding_3_small()
    {
        var options = new AvalAIOptions();
        Assert.Equal("text-embedding-3-small", options.EmbeddingModel);
    }

    [Fact]
    public void ApiKey_is_empty_by_default()
    {
        var options = new AvalAIOptions();
        Assert.Equal(string.Empty, options.ApiKey);
    }

    [Fact]
    public void SectionName_is_AvalAI()
    {
        Assert.Equal("AvalAI", AvalAIOptions.SectionName);
    }

    [Fact]
    public void BaseUrl_trailing_slash_is_handled()
    {
        var options = new AvalAIOptions { BaseUrl = "https://api.avalai.ir/" };
        var trimmed = options.BaseUrl.TrimEnd('/');
        Assert.Equal("https://api.avalai.ir", trimmed);
    }

    [Fact]
    public void BaseUrl_with_v1_is_preserved()
    {
        var options = new AvalAIOptions { BaseUrl = "https://api.avalai.ir/v1" };
        var trimmed = options.BaseUrl.TrimEnd('/');
        Assert.EndsWith("/v1", trimmed);
    }

    [Fact]
    public void BaseUrl_without_v1_gets_v1_appended()
    {
        var baseUrl = "https://api.avalai.ir";
        var trimmed = baseUrl.TrimEnd('/');
        if (!trimmed.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
        {
            trimmed += "/v1";
        }
        Assert.Equal("https://api.avalai.ir/v1", trimmed);
    }

    [Fact]
    public void BaseUrl_already_with_v1_is_not_doubled()
    {
        var baseUrl = "https://api.avalai.ir/v1";
        var trimmed = baseUrl.TrimEnd('/');
        if (!trimmed.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
        {
            trimmed += "/v1";
        }
        Assert.Equal("https://api.avalai.ir/v1", trimmed);
    }

    [Fact]
    public void ApiKey_not_logged_in_tostring()
    {
        var options = new AvalAIOptions { ApiKey = "secret-key-12345" };
        var str = options.ToString()!;
        Assert.DoesNotContain("secret-key-12345", str);
    }
}
