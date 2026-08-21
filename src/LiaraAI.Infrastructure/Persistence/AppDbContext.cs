using LiaraAI.Domain.Conversations;
using LiaraAI.Domain.Documents;
using Microsoft.EntityFrameworkCore;

namespace LiaraAI.Infrastructure.Persistence;

/// <summary>
/// Primary EF Core database context for Liara AI.
/// pgvector is enabled at the model level and on the Npgsql data source
/// (see <see cref="DependencyInjection"/>).
/// </summary>
public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Document> Documents => Set<Document>();

    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();

    public DbSet<Conversation> Conversations => Set<Conversation>();

    public DbSet<Message> Messages => Set<Message>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Register the pgvector extension so vector columns are supported.
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
