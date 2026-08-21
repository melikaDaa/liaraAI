using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using LiaraAI.Application.Embeddings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LiaraAI.Infrastructure.Embeddings;

/// <summary>
/// AvalAI-backed <see cref="IEmbeddingService"/>.
///
/// Endpoint (per official docs, https://docs.avalai.ir/fa/api-reference/embeddings):
///   POST {BaseUrl}/embeddings
///   Auth:  Authorization: Bearer &lt;ApiKey&gt;
///   Body:  { "model": "&lt;model&gt;", "input": ["text", ...], "encoding_format": "float" }
///   Resp:  { "data": [ { "embedding": [..], "index": 0 }, ... ], "model": "..", "usage": {..} }
///
/// The API supports batching multiple inputs via the "input" array; results are
/// returned in an "index" field and are re-ordered here to match input order.
/// </summary>
public sealed class AvalAIEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly AvalAIOptions _options;
    private readonly ILogger<AvalAIEmbeddingService> _logger;

    public AvalAIEmbeddingService(
        HttpClient httpClient,
        IOptions<AvalAIOptions> options,
        ILogger<AvalAIEmbeddingService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<float[]>> EmbedAsync(
        IReadOnlyList<string> inputs,
        CancellationToken cancellationToken = default)
    {
        if (inputs.Count == 0)
        {
            return Array.Empty<float[]>();
        }

        var request = new EmbeddingRequest
        {
            Model = _options.EmbeddingModel,
            Input = inputs,
            EncodingFormat = "float"
        };

        using var response = await _httpClient.PostAsJsonAsync("embeddings", request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            _logger.LogWarning("AvalAI rate limit (429) hit for embeddings request.");
            throw new AvalAIEmbeddingException("AvalAI returned 429 Too Many Requests.", (int)response.StatusCode);
        }

        if (!response.IsSuccessStatusCode)
        {
            // Do not log the request body (may be large / sensitive); log status only.
            _logger.LogError("AvalAI embeddings request failed with status {Status}.", (int)response.StatusCode);
            throw new AvalAIEmbeddingException(
                $"AvalAI embeddings request failed with status {(int)response.StatusCode}.",
                (int)response.StatusCode);
        }

        EmbeddingResponse? payload;
        try
        {
            payload = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(cancellationToken);
        }
        catch (Exception ex)
        {
            throw new AvalAIEmbeddingException("AvalAI embeddings response could not be parsed.", ex);
        }

        if (payload?.Data is null || payload.Data.Count == 0)
        {
            throw new AvalAIEmbeddingException("AvalAI embeddings response contained no data.");
        }

        // Re-order by the response index so vectors align with the input order.
        var ordered = new float[inputs.Count][];
        foreach (var item in payload.Data)
        {
            if (item.Index < 0 || item.Index >= inputs.Count || item.Embedding is null)
            {
                throw new AvalAIEmbeddingException(
                    $"AvalAI returned an out-of-range or empty embedding (index {item.Index}).");
            }

            ordered[item.Index] = item.Embedding;
        }

        if (ordered.Any(v => v is null))
        {
            throw new AvalAIEmbeddingException("AvalAI response was missing one or more embeddings.");
        }

        return ordered;
    }

    private sealed class EmbeddingRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("input")]
        public IReadOnlyList<string> Input { get; set; } = Array.Empty<string>();

        [JsonPropertyName("encoding_format")]
        public string EncodingFormat { get; set; } = "float";
    }

    private sealed class EmbeddingResponse
    {
        [JsonPropertyName("data")]
        public List<EmbeddingData>? Data { get; set; }

        [JsonPropertyName("model")]
        public string? Model { get; set; }
    }

    private sealed class EmbeddingData
    {
        [JsonPropertyName("embedding")]
        public float[]? Embedding { get; set; }

        [JsonPropertyName("index")]
        public int Index { get; set; }
    }
}
