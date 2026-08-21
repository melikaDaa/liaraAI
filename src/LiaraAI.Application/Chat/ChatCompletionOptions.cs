namespace LiaraAI.Application.Chat;

/// <summary>
/// Configuration for the chat completion service. Bound from "AvalAI" section.
/// </summary>
public sealed class ChatCompletionOptions
{
    public const string SectionName = "AvalAI";

    /// <summary>Chat model identifier (e.g. gpt-4o-mini).</summary>
    public string ChatModel { get; set; } = "gpt-4o-mini";

    /// <summary>Maximum tokens for the response.</summary>
    public int MaxTokens { get; set; } = 2048;

    /// <summary>Sampling temperature.</summary>
    public double Temperature { get; set; } = 0.3;
}
