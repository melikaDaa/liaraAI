using LiaraAI.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LiaraAI.Infrastructure.Persistence.Configurations;

public sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("documents");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Title)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(d => d.Url)
            .HasMaxLength(2048);

        builder.Property(d => d.Path)
            .IsRequired()
            .HasMaxLength(1024);

        builder.Property(d => d.Category)
            .HasMaxLength(256);

        builder.Property(d => d.Content)
            .IsRequired();

        builder.Property(d => d.CreatedAt)
            .IsRequired();

        builder.Property(d => d.UpdatedAt)
            .IsRequired();

        builder.HasIndex(d => d.Url).HasDatabaseName("ix_documents_url");
        builder.HasIndex(d => d.Path).IsUnique().HasDatabaseName("ix_documents_path");

        builder.HasMany(d => d.Chunks)
            .WithOne(c => c.Document)
            .HasForeignKey(c => c.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
