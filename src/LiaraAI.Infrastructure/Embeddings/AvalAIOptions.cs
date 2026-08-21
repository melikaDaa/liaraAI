namespace LiaraAI.Infrastructure.Embeddings;

/// <summary>
/// Configuration for the AvalAI provider. Bound from the "AvalAI" section.
/// The API key must come from secure configuration / environment variables and
/// must never be committed or logged.
/// </summary>
public sealed class AvalAIOptions
{
    public const string SectionName = "AvalAI";

    /// <summary>OpenAI-compatible base URL. Example: https://api.avalai.ir</summary>
    public string BaseUrl { get; set; } = "https://api.avalai.ir";

    /// <summary>Secret API key (Authorization: Bearer). Never log this value.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Embedding model id. Default returns 1536-dim vectors.</summary>
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";

    /// <summary>HTTP request timeout in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 100;
}
