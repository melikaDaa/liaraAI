using LiaraAI.Application.Documentation;
using LiaraAI.Domain.Documents;
using LiaraAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LiaraAI.Infrastructure.Documentation;

/// <summary>
/// EF Core implementation of <see cref="IDocumentIngestionStore"/>.
/// Upserts a document by its unique relative path and replaces its chunks.
/// </summary>
public sealed class EfDocumentIngestionStore : IDocumentIngestionStore
{
    private readonly AppDbContext _dbContext;

    public EfDocumentIngestionStore(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task UpsertAsync(Document document, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.Documents
            .Include(d => d.Chunks)
            .FirstOrDefaultAsync(d => d.Path == document.Path, cancellationToken);

        if (existing is null)
        {
            _dbContext.Documents.Add(document);
        }
        else
        {
            existing.Title = document.Title;
            existing.Url = document.Url;
            existing.Category = document.Category;
            existing.Content = document.Content;
            existing.UpdatedAt = DateTimeOffset.UtcNow;

            _dbContext.DocumentChunks.RemoveRange(existing.Chunks);
            existing.Chunks = document.Chunks;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
