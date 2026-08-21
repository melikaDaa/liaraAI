using LiaraAI.Application.Chat;
using LiaraAI.Application.Conversations;
using LiaraAI.Application.Documentation;
using LiaraAI.Application.Documentation.Parsing;
using LiaraAI.Application.Embeddings;
using LiaraAI.Application.Rag;
using LiaraAI.Application.Search;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LiaraAI.Application;

/// <summary>
/// Registers Application-layer services (documentation ingestion pipeline, RAG pipeline).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<DocumentationOptions>()
            .Bind(configuration.GetSection(DocumentationOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.SourcePath),
                "Documentation:SourcePath must be configured.")
            .Validate(o => o.Chunking.MaxCharacters > 0,
                "Documentation:Chunking:MaxCharacters must be greater than zero.")
            .ValidateOnStart();

        services.AddSingleton<MarkdownParser>();
        services.AddScoped<IDocumentIngestionService, DocumentIngestionService>();

        services.AddOptions<EmbeddingProcessingOptions>()
            .Bind(configuration.GetSection(EmbeddingProcessingOptions.SectionName))
            .Validate(o => o.BatchSize > 0, "Embeddings:BatchSize must be greater than zero.")
            .Validate(o => o.Dimensions > 0, "Embeddings:Dimensions must be greater than zero.");

        services.AddScoped<IChunkEmbeddingService, ChunkEmbeddingService>();

        services.AddOptions<RagOptions>()
            .Bind(configuration.GetSection(RagOptions.SectionName))
            .Validate(o => o.TopK > 0, "Rag:TopK must be greater than zero.")
            .Validate(o => o.MinSimilarity >= 0 && o.MinSimilarity <= 1,
                "Rag:MinSimilarity must be between 0 and 1.")
            .Validate(o => o.MaxContextCharacters > 0,
                "Rag:MaxContextCharacters must be greater than zero.")
            .Validate(o => o.MaxHistoryMessages >= 0,
                "Rag:MaxHistoryMessages must be non-negative.")
            .Validate(o => o.MaxMessageLength > 0,
                "Rag:MaxMessageLength must be greater than zero.")
            .ValidateOnStart();

        services.AddScoped<IContextBuilder, DocumentationContextBuilder>();
        services.AddSingleton<IConversationStore>(sp =>
        {
            return new InMemoryConversationStore(maxMessagesPerConversation: 50);
        });
        services.AddScoped<IRagService, RagService>();

        services.AddScoped<IConversationService, ConversationService>();

        return services;
    }
}
