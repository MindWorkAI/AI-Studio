namespace AIStudio.Provider.Google;

public sealed record GoogleEmbedding
{
    public List<float>? Values { get; init; }
}