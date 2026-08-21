using LiaraAI.Domain.Conversations;
using LiaraAI.Domain.Documents;
using LiaraAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LiaraAI.IntegrationTests.Persistence;

/// <summary>
/// Verifies the EF Core model built from the Infrastructure configurations,
/// without requiring a live PostgreSQL connection. Building the model against
/// the Npgsql provider exercises the pgvector column type and index mappings.
/// </summary>
public class AppDbContextModelTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=liaraai;Username=liaraai;Password=test",
                npgsql => npgsql.UseVector())
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public void Document_and_DocumentChunk_are_mapped()
    {
        using var context = CreateContext();
        var model = context.Model;

        Assert.NotNull(model.FindEntityType(typeof(Document)));
        Assert.NotNull(model.FindEntityType(typeof(DocumentChunk)));
    }

    [Fact]
    public void Conversation_and_Message_are_mapped()
    {
        using var context = CreateContext();
        var model = context.Model;

        Assert.NotNull(model.FindEntityType(typeof(Conversation)));
        Assert.NotNull(model.FindEntityType(typeof(Message)));
    }

    [Fact]
    public void Documents_map_to_expected_tables()
    {
        using var context = CreateContext();

        Assert.Equal("documents", context.Model.FindEntityType(typeof(Document))!.GetTableName());
        Assert.Equal("document_chunks", context.Model.FindEntityType(typeof(DocumentChunk))!.GetTableName());
    }

    [Fact]
    public void Conversations_map_to_expected_table()
    {
        using var context = CreateContext();

        Assert.Equal("conversations", context.Model.FindEntityType(typeof(Conversation))!.GetTableName());
        Assert.Equal("messages", context.Model.FindEntityType(typeof(Message))!.GetTableName());
    }

    [Fact]
    public void Embedding_column_uses_pgvector_1536()
    {
        using var context = CreateContext();

        var embedding = context.Model
            .FindEntityType(typeof(DocumentChunk))!
            .FindProperty(nameof(DocumentChunk.Embedding))!;

        Assert.Equal($"vector({DocumentChunk.EmbeddingDimensions})", embedding.GetColumnType());
        Assert.True(embedding.IsNullable);
    }

    [Fact]
    public void DocumentChunk_has_index_on_document_id()
    {
        using var context = CreateContext();

        var chunk = context.Model.FindEntityType(typeof(DocumentChunk))!;
        var fkProperty = chunk.FindProperty(nameof(DocumentChunk.DocumentId))!;

        var hasIndex = chunk.GetIndexes()
            .Any(i => i.Properties.Any(p => p == fkProperty));

        Assert.True(hasIndex);
    }

    [Fact]
    public void Document_has_indexes_on_url_and_path()
    {
        using var context = CreateContext();

        var document = context.Model.FindEntityType(typeof(Document))!;
        var indexedColumns = document.GetIndexes()
            .SelectMany(i => i.Properties)
            .Select(p => p.Name)
            .ToHashSet();

        Assert.Contains(nameof(Document.Url), indexedColumns);
        Assert.Contains(nameof(Document.Path), indexedColumns);
    }

    [Fact]
    public void Document_to_chunks_is_one_to_many_with_cascade_delete()
    {
        using var context = CreateContext();

        var chunk = context.Model.FindEntityType(typeof(DocumentChunk))!;
        var fk = chunk.GetForeignKeys().Single();

        Assert.Equal(typeof(Document), fk.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Cascade, fk.DeleteBehavior);
    }

    [Fact]
    public void Message_to_conversation_is_one_to_many_with_cascade_delete()
    {
        using var context = CreateContext();

        var message = context.Model.FindEntityType(typeof(Message))!;
        var fk = message.GetForeignKeys().Single();

        Assert.Equal(typeof(Conversation), fk.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Cascade, fk.DeleteBehavior);
    }

    [Fact]
    public void Message_has_indexes_on_conversation_id_and_created_at()
    {
        using var context = CreateContext();

        var message = context.Model.FindEntityType(typeof(Message))!;
        var indexedColumns = message.GetIndexes()
            .SelectMany(i => i.Properties)
            .Select(p => p.Name)
            .ToHashSet();

        Assert.Contains(nameof(Message.ConversationId), indexedColumns);
        Assert.Contains(nameof(Message.CreatedAt), indexedColumns);
    }
}
