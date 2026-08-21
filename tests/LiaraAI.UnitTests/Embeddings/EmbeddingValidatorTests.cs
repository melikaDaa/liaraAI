using LiaraAI.Application.Embeddings;

namespace LiaraAI.UnitTests.Embeddings;

public class EmbeddingValidatorTests
{
    private static float[] Vector(int length, float value = 0.1f)
    {
        var v = new float[length];
        Array.Fill(v, value);
        return v;
    }

    [Fact]
    public void Valid_1536_vector_passes()
    {
        var ok = EmbeddingValidator.TryValidate(Vector(1536), 1536, out var error);

        Assert.True(ok);
        Assert.Null(error);
    }

    [Fact]
    public void Null_vector_fails()
    {
        var ok = EmbeddingValidator.TryValidate(null, 1536, out var error);

        Assert.False(ok);
        Assert.Contains("null", error);
    }

    [Fact]
    public void Wrong_dimension_fails_without_padding_or_truncation()
    {
        var ok = EmbeddingValidator.TryValidate(Vector(768), 1536, out var error);

        Assert.False(ok);
        Assert.Contains("768", error);
        Assert.Contains("1536", error);
    }

    [Fact]
    public void NaN_value_fails()
    {
        var v = Vector(1536);
        v[10] = float.NaN;

        var ok = EmbeddingValidator.TryValidate(v, 1536, out var error);

        Assert.False(ok);
        Assert.Contains("non-finite", error);
    }

    [Fact]
    public void Infinity_value_fails()
    {
        var v = Vector(1536);
        v[0] = float.PositiveInfinity;

        var ok = EmbeddingValidator.TryValidate(v, 1536, out var error);

        Assert.False(ok);
        Assert.Contains("non-finite", error);
    }
}
