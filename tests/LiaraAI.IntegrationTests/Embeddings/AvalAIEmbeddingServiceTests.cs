using System.Net;
using System.Text;
using System.Text.Json;
using LiaraAI.Application.Embeddings;
using LiaraAI.Infrastructure.Embeddings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LiaraAI.IntegrationTests.Embeddings;

public class AvalAIEmbeddingServiceTests
{
    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public int Calls { get; private set; }

        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(_responder(request));
        }
    }

    private static AvalAIEmbeddingService CreateService(FakeHandler handler)
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.avalai.ir/v1/") };
        var options = Options.Create(new AvalAIOptions
        {
            BaseUrl = "https://api.avalai.ir/v1",
            ApiKey = "test-key",
            EmbeddingModel = "text-embedding-3-small"
        });
        return new AvalAIEmbeddingService(client, options, NullLogger<AvalAIEmbeddingService>.Instance);
    }

    private static string BuildResponse(params (int index, float[] vector)[] items)
    {
        var data = items.Select(i => new
        {
            @object = "embedding",
            embedding = i.vector,
            index = i.index
        });

        return JsonSerializer.Serialize(new
        {
            @object = "list",
            data,
            model = "text-embedding-3-small",
            usage = new { prompt_tokens = 8, total_tokens = 8 }
        });
    }

    private static HttpResponseMessage Ok(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task Parses_embeddings_and_preserves_input_order()
    {
        var v0 = Enumerable.Repeat(0.1f, 1536).ToArray();
        var v1 = Enumerable.Repeat(0.2f, 1536).ToArray();

        // Return out of order to prove we re-order by index.
        var handler = new FakeHandler(_ => Ok(BuildResponse((1, v1), (0, v0))));
        var service = CreateService(handler);

        var result = await service.EmbedAsync(new[] { "first", "second" });

        Assert.Equal(2, result.Count);
        Assert.Equal(0.1f, result[0][0]);
        Assert.Equal(0.2f, result[1][0]);
    }

    [Fact]
    public async Task Posts_to_correct_embeddings_endpoint()
    {
        HttpRequestMessage? captured = null;
        var v0 = Enumerable.Repeat(0.1f, 1536).ToArray();
        var handler = new FakeHandler(req =>
        {
            captured = req;
            return Ok(BuildResponse((0, v0)));
        });
        var service = CreateService(handler);

        await service.EmbedAsync(new[] { "hello" });

        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Post, captured!.Method);
        Assert.EndsWith("/embeddings", captured.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task Empty_input_short_circuits_without_calling_api()
    {
        var handler = new FakeHandler(_ => Ok(BuildResponse()));
        var service = CreateService(handler);

        var result = await service.EmbedAsync(Array.Empty<string>());

        Assert.Empty(result);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Http_500_throws_provider_exception()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var service = CreateService(handler);

        await Assert.ThrowsAnyAsync<EmbeddingProviderException>(
            () => service.EmbedAsync(new[] { "x" }));
    }

    [Fact]
    public async Task Rate_limit_429_throws_provider_exception_with_status()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        var service = CreateService(handler);

        var ex = await Assert.ThrowsAnyAsync<EmbeddingProviderException>(
            () => service.EmbedAsync(new[] { "x" }));
        Assert.Equal(429, ex.StatusCode);
    }

    [Fact]
    public async Task Malformed_json_throws_provider_exception()
    {
        var handler = new FakeHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{ not json", Encoding.UTF8, "application/json")
            });
        var service = CreateService(handler);

        await Assert.ThrowsAnyAsync<EmbeddingProviderException>(
            () => service.EmbedAsync(new[] { "x" }));
    }

    [Fact]
    public async Task Missing_embedding_for_input_throws()
    {
        // Only one embedding returned for two inputs.
        var v0 = Enumerable.Repeat(0.1f, 1536).ToArray();
        var handler = new FakeHandler(_ => Ok(BuildResponse((0, v0))));
        var service = CreateService(handler);

        await Assert.ThrowsAnyAsync<EmbeddingProviderException>(
            () => service.EmbedAsync(new[] { "a", "b" }));
    }
}
