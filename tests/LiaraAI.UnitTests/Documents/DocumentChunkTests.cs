using LiaraAI.Domain.Documents;

namespace LiaraAI.UnitTests.Documents;

public class DocumentChunkTests
{
    [Fact]
    public void EmbeddingDimensions_matches_text_embedding_3_small()
    {
        Assert.Equal(1536, DocumentChunk.EmbeddingDimensions);
    }

    [Fact]
    public void Embedding_is_null_by_default()
    {
        var chunk = new DocumentChunk();

        Assert.Null(chunk.Embedding);
    }

    [Fact]
    public void New_document_initializes_ids_timestamps_and_empty_chunks()
    {
        var document = new Document();

        Assert.NotEqual(Guid.Empty, document.Id);
        Assert.Empty(document.Chunks);
        Assert.NotEqual(default, document.CreatedAt);
        Assert.NotEqual(default, document.UpdatedAt);
    }
}
