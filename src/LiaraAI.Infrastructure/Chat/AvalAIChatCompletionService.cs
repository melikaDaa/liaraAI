using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using LiaraAI.Application.Chat;
using LiaraAI.Application.Search;
using Microsoft.Extensions.Logging;

namespace LiaraAI.Infrastructure.Chat;

/// <summary>
/// AvalAI-backed chat completion service. Uses the OpenAI-compatible
/// /chat/completions endpoint.
/// </summary>
public sealed class AvalAIChatCompletionService : IChatCompletionService
{
    private readonly HttpClient _httpClient;
    private readonly ChatCompletionOptions _options;
    private readonly ILogger<AvalAIChatCompletionService> _logger;

    public AvalAIChatCompletionService(
        HttpClient httpClient,
        Microsoft.Extensions.Options.IOptions<ChatCompletionOptions> options,
        ILogger<AvalAIChatCompletionService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> CompleteAsync(
        IReadOnlyList<ChatMessage> messages,
        CancellationToken cancellationToken = default)
    {
        if (messages.Count == 0)
        {
            throw new ArgumentException("Messages must not be empty.", nameof(messages));
        }

        var request = new ChatRequest
        {
            Model = _options.ChatModel,
            Messages = messages.Select(m => new Message { Role = m.Role, Content = m.Content }).ToList(),
            MaxTokens = _options.MaxTokens,
            Temperature = _options.Temperature
        };

        using var response = await _httpClient.PostAsJsonAsync("chat/completions", request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            _logger.LogWarning("AvalAI rate limit (429) hit for chat completion.");
            throw new HttpRequestException("AvalAI returned 429 Too Many Requests.", null, HttpStatusCode.TooManyRequests);
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("AvalAI chat completion failed with status {Status}: {Body}", (int)response.StatusCode, body);
            throw new HttpRequestException($"AvalAI chat completion failed with status {(int)response.StatusCode}.");
        }

        ChatResponse? payload;
        try
        {
            payload = await response.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse AvalAI chat completion response.");
            throw new InvalidOperationException("AvalAI chat completion response could not be parsed.", ex);
        }

        if (payload?.Choices is null || payload.Choices.Count == 0)
        {
            throw new InvalidOperationException("AvalAI chat completion response contained no choices.");
        }

        return payload.Choices[0].Message?.Content ?? string.Empty;
    }

    private sealed class ChatRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("messages")]
        public List<Message> Messages { get; set; } = new();

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; } = 2048;

        [JsonPropertyName("temperature")]
        public double Temperature { get; set; } = 0.3;
    }

    private sealed class Message
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    private sealed class ChatResponse
    {
        [JsonPropertyName("choices")]
        public List<Choice>? Choices { get; set; }
    }

    private sealed class Choice
    {
        [JsonPropertyName("message")]
        public Message? Message { get; set; }

        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; }
    }
}
