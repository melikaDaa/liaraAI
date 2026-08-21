using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using Pgvector.Npgsql;
using LiaraAI.Application.Chat;
using LiaraAI.Application.Conversations;
using LiaraAI.Application.Documentation;
using LiaraAI.Application.Embeddings;
using LiaraAI.Application.Search;
using LiaraAI.Infrastructure.Chat;
using LiaraAI.Infrastructure.Conversations;
using LiaraAI.Infrastructure.Documentation;
using LiaraAI.Infrastructure.Embeddings;
using LiaraAI.Infrastructure.Persistence;
using LiaraAI.Infrastructure.Search;

namespace LiaraAI.Infrastructure;

/// <summary>
/// Registers Infrastructure-level services (PostgreSQL, Redis, health checks)
/// so the API layer only depends on this single composition entry point.
/// </summary>
public static class DependencyInjection
{
    public const string PostgresHealthCheckName = "postgres";
    public const string RedisHealthCheckName = "redis";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var postgresConnectionString =
            configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException(
                "Connection string 'Postgres' is not configured. " +
                "Set ConnectionStrings__Postgres (see .env.example).");

        var redisConnectionString =
            configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException(
                "Connection string 'Redis' is not configured. " +
                "Set ConnectionStrings__Redis (see .env.example).");

        // PostgreSQL + pgvector via EF Core.
        // The NpgsqlDataSource is built explicitly so the pgvector plugin is
        // registered at the ADO.NET level too: raw-SQL Vector parameters
        // (e.g. PgVectorSearchService) are then mapped correctly, not just
        // EF model properties.
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(postgresConnectionString);
        dataSourceBuilder.UseVector();
        var postgresDataSource = dataSourceBuilder.Build();

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(
                postgresDataSource,
                npgsql => npgsql.UseVector()));

        // Redis distributed cache (optional for correctness, used for caching/state later).
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnectionString;
            options.InstanceName = "liaraai:";
        });

        // Documentation ingestion: local filesystem source + EF Core store.
        services.AddScoped<IDocumentFileSource, FileSystemDocumentFileSource>();
        services.AddScoped<IDocumentIngestionStore, EfDocumentIngestionStore>();

        // Embeddings: AvalAI provider (typed HttpClient) + EF Core chunk store.
        services.AddOptions<AvalAIOptions>()
            .Bind(configuration.GetSection(AvalAIOptions.SectionName))
            .PostConfigure(o =>
            {
                // Allow the standard AVALAI_API_KEY env var to supply the key when
                // the AvalAI:ApiKey section value is empty. The key is never logged.
                if (string.IsNullOrWhiteSpace(o.ApiKey))
                {
                    o.ApiKey = configuration["AVALAI_API_KEY"] ?? string.Empty;
                }
            })
            .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl), "AvalAI:BaseUrl must be configured.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.EmbeddingModel), "AvalAI:EmbeddingModel must be configured.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.ApiKey),
                "AvalAI:ApiKey must be configured. Set AVALAI_API_KEY environment variable or AvalAI:ApiKey in configuration. See .env.example.")
            .ValidateOnStart();

        services.AddScoped<IChunkEmbeddingStore, EfChunkEmbeddingStore>();

        services.AddHttpClient<IEmbeddingService, AvalAIEmbeddingService>((provider, client) =>
        {
            var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AvalAIOptions>>().Value;

            var baseUrl = options.BaseUrl.TrimEnd('/');
            if (!baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            {
                baseUrl += "/v1";
            }
            client.BaseAddress = new Uri(baseUrl + "/");
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);

            if (!string.IsNullOrWhiteSpace(options.ApiKey))
            {
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.ApiKey);
            }
        });

        services.AddOptions<ChatCompletionOptions>()
            .Bind(configuration.GetSection(ChatCompletionOptions.SectionName))
            .PostConfigure(o =>
            {
                if (string.IsNullOrWhiteSpace(o.ChatModel))
                {
                    o.ChatModel = "gpt-4o-mini";
                }
            })
            .Validate(o => !string.IsNullOrWhiteSpace(o.ChatModel), "AvalAI:ChatModel must be configured.");

        services.AddHttpClient<IChatCompletionService, AvalAIChatCompletionService>((provider, client) =>
        {
            var avalaiOptions = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AvalAIOptions>>().Value;
            var chatOptions = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ChatCompletionOptions>>().Value;

            var baseUrl = avalaiOptions.BaseUrl.TrimEnd('/');
            if (!baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            {
                baseUrl += "/v1";
            }
            client.BaseAddress = new Uri(baseUrl + "/");
            client.Timeout = TimeSpan.FromSeconds(Math.Max(avalaiOptions.TimeoutSeconds, 120));

            if (!string.IsNullOrWhiteSpace(avalaiOptions.ApiKey))
            {
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", avalaiOptions.ApiKey);
            }
        });

        services.AddScoped<ISearchService, PgVectorSearchService>();

        // Conversation persistence.
        services.AddScoped<IConversationRepository, PostgresConversationRepository>();

        // Health checks: API self-check (liveness) + PostgreSQL + Redis (readiness).
        services.AddHealthChecks()
            .AddCheck("api", () => HealthCheckResult.Healthy(), tags: new[] { "live" })
            .AddNpgSql(
                postgresConnectionString,
                name: PostgresHealthCheckName,
                tags: new[] { "ready", "db" })
            .AddRedis(
                redisConnectionString,
                name: RedisHealthCheckName,
                tags: new[] { "ready", "cache" });

        return services;
    }
}
