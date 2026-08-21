using System.Text.Json;
using LiaraAI.Api.Configuration;
using LiaraAI.Application;
using LiaraAI.Application.Chat;
using LiaraAI.Application.Conversations;
using LiaraAI.Application.Documentation;
using LiaraAI.Application.Embeddings;
using LiaraAI.Application.Rag;
using LiaraAI.Infrastructure;
using LiaraAI.Infrastructure.Conversations;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Load .env file into configuration so local development works without
// manually setting environment variables in every terminal session.
// Environment variables already set in the OS take precedence.
var envFilePath = Path.Combine(builder.Environment.ContentRootPath, "..", "..", ".env");
if (File.Exists(envFilePath))
{
    var envConfig = new Dictionary<string, string?>();
    envConfig.LoadEnvFile(envFilePath);
    builder.Configuration.AddInMemoryCollection(envConfig);
}

builder.Services.AddOpenApi();
builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = WriteHealthResponse
});

// POST /api/chat - RAG-powered chat endpoint.
app.MapPost("/api/chat", async (
    ChatRequest request,
    IRagService ragService,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Message))
    {
        return Results.BadRequest(new { error = "Message is required." });
    }

    try
    {
        var ragRequest = new RagRequest
        {
            Message = request.Message,
            ConversationId = request.ConversationId
        };

        var response = await ragService.AskAsync(ragRequest, cancellationToken);

        var dto = new
        {
            answer = response.Answer,
            conversationId = request.ConversationId ?? Guid.NewGuid().ToString("N"),
            sources = response.Sources.Select(s => new
            {
                title = s.Title,
                url = s.Url,
                heading = s.Heading,
                headingPath = s.HeadingPath,
                similarity = Math.Round(s.Similarity, 4)
            })
        };

        return Results.Ok(dto);
    }
    catch (Exception)
    {
        return Results.Json(
            new { error = "متأسفانه خطایی رخ داد. لطفاً بعداً دوباره تلاش کنید." },
            statusCode: 500);
    }
})
.WithName("Chat")
.WithOpenApi();

if (app.Environment.IsDevelopment())
{
    app.MapPost("/admin/ingest", async (
        IDocumentIngestionService ingestion,
        CancellationToken cancellationToken) =>
    {
        var result = await ingestion.IngestAsync(cancellationToken);
        return Results.Ok(result);
    });

    app.MapPost("/admin/embed", async (
        IChunkEmbeddingService embedding,
        CancellationToken cancellationToken) =>
    {
        var result = await embedding.BackfillAsync(cancellationToken);
        return Results.Ok(result);
    });
}

// Conversation endpoints
app.MapGet("/api/conversations", async (
    IConversationService conversationService,
    CancellationToken cancellationToken) =>
{
    var conversations = await conversationService.GetAllAsync(cancellationToken);
    var dtos = conversations.Select(c => new
    {
        id = c.Id,
        title = c.Title,
        createdAt = c.CreatedAt,
        updatedAt = c.UpdatedAt
    });
    return Results.Ok(dtos);
})
.WithName("GetConversations")
.WithOpenApi();

app.MapGet("/api/conversations/{id}", async (
    Guid id,
    IConversationService conversationService,
    CancellationToken cancellationToken) =>
{
    var conversation = await conversationService.GetByIdAsync(id, cancellationToken);
    if (conversation == null)
        return Results.NotFound();

    var dto = new
    {
        id = conversation.Id,
        title = conversation.Title,
        createdAt = conversation.CreatedAt,
        updatedAt = conversation.UpdatedAt,
        messages = conversation.Messages.Select(m => new
        {
            id = m.Id,
            role = m.Role,
            content = m.Content,
            createdAt = m.CreatedAt
        })
    };
    return Results.Ok(dto);
})
.WithName("GetConversation")
.WithOpenApi();

app.MapPost("/api/conversations", async (
    CreateConversationRequest request,
    IConversationService conversationService,
    CancellationToken cancellationToken) =>
{
    var title = ConversationService.GenerateTitle(request.Title ?? string.Empty);
    var conversation = await conversationService.CreateAsync(title, cancellationToken);

    var dto = new
    {
        id = conversation.Id,
        title = conversation.Title,
        createdAt = conversation.CreatedAt,
        updatedAt = conversation.UpdatedAt
    };
    return Results.Created($"/api/conversations/{conversation.Id}", dto);
})
.WithName("CreateConversation")
.WithOpenApi();

app.MapDelete("/api/conversations/{id}", async (
    Guid id,
    IConversationService conversationService,
    CancellationToken cancellationToken) =>
{
    await conversationService.DeleteAsync(id, cancellationToken);
    return Results.NoContent();
})
.WithName("DeleteConversation")
.WithOpenApi();

app.MapGet("/api/conversations/{id}/messages", async (
    Guid id,
    IConversationRepository repository,
    CancellationToken cancellationToken) =>
{
    var conversation = await repository.GetByIdAsync(id, cancellationToken);
    if (conversation == null)
        return Results.NotFound();

    var messages = await repository.GetMessagesAsync(id, 50, cancellationToken);
    var dtos = messages.Select(m => new
    {
        id = m.Id,
        role = m.Role,
        content = m.Content,
        createdAt = m.CreatedAt
    });
    return Results.Ok(dtos);
})
.WithName("GetConversationMessages")
.WithOpenApi();

app.Run();

static Task WriteHealthResponse(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";
    var payload = new
    {
        status = report.Status.ToString(),
        totalDurationMs = report.TotalDuration.TotalMilliseconds,
        checks = report.Entries.Select(entry => new
        {
            name = entry.Key,
            status = entry.Value.Status.ToString(),
            durationMs = entry.Value.Duration.TotalMilliseconds
        })
    };
    return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
}
