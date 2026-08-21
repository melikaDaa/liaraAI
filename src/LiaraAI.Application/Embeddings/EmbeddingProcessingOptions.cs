namespace LiaraAI.Application.Embeddings;

/// <summary>
/// Options controlling the embedding backfill process. Bound from the
/// "Embeddings" configuration section. Model/provider secrets live in the
/// AvalAI configuration in Infrastructure, not here.
/// </summary>
public sealed class EmbeddingProcessingOptions
{
    public const string SectionName = "Embeddings";

    /// <summary>Number of chunks embedded per AvalAI request (array input).</summary>
    public int BatchSize { get; set; } = 64;

    /// <summary>Max retry attempts per batch on transient failures.</summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>Base delay (ms) for exponential backoff between retries.</summary>
    public int RetryBaseDelayMs { get; set; } = 1000;

    /// <summary>Expected vector dimension; must match the pgvector column.</summary>
    public int Dimensions { get; set; } = EmbeddingValidator.RequiredDimensions;
}
