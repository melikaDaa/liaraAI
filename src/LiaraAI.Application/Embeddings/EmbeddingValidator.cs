using LiaraAI.Domain.Documents;

namespace LiaraAI.Application.Embeddings;

/// <summary>
/// Validates embedding vectors before persistence. Enforces the fixed schema
/// dimension and rejects non-finite values. Never mutates the vector.
/// </summary>
public static class EmbeddingValidator
{
    /// <summary>Required dimension, tied to the pgvector(1536) schema column.</summary>
    public const int RequiredDimensions = DocumentChunk.EmbeddingDimensions;

    public static bool TryValidate(float[]? vector, int expectedDimensions, out string? error)
    {
        if (vector is null)
        {
            error = "Embedding vector is null.";
            return false;
        }

        if (vector.Length != expectedDimensions)
        {
            error = $"Embedding dimension {vector.Length} does not match required {expectedDimensions}.";
            return false;
        }

        for (var i = 0; i < vector.Length; i++)
        {
            if (float.IsNaN(vector[i]) || float.IsInfinity(vector[i]))
            {
                error = $"Embedding contains a non-finite value at index {i}.";
                return false;
            }
        }

        error = null;
        return true;
    }
}
