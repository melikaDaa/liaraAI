using LiaraAI.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace LiaraAI.Infrastructure.Persistence.Configurations;

public sealed class DocumentChunkConfiguration : IEntityTypeConfiguration<DocumentChunk>
{
    public void Configure(EntityTypeBuilder<DocumentChunk> builder)
    {
        builder.ToTable("document_chunks");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Content)
            .IsRequired();

        builder.Property(c => c.ChunkIndex)
            .IsRequired();

        builder.Property(c => c.Heading)
            .HasMaxLength(512);

        builder.Property(c => c.HeadingPath)
            .HasMaxLength(2048);

        builder.Property(c => c.CharacterCount)
            .IsRequired();

        // pgvector column sized for text-embedding-3-small (1536 dims).
        // Mapped from the Domain's float[] to a pgvector Vector via a value converter,
        // keeping the Domain free of infrastructure types. Nullable until computed.
        builder.Property(c => c.Embedding)
            .HasColumnType($"vector({DocumentChunk.EmbeddingDimensions})")
            .HasConversion(
                v => v == null ? null : new Vector(v),
                v => v == null ? null : v.ToArray());

        builder.HasIndex(c => c.DocumentId).HasDatabaseName("ix_document_chunks_document_id");

        builder.HasIndex(c => new { c.DocumentId, c.ChunkIndex })
            .IsUnique()
            .HasDatabaseName("ix_document_chunks_document_id_chunk_index");
    }
}
