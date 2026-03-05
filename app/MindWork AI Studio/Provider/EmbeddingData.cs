// ReSharper disable CollectionNeverUpdated.Global
namespace AIStudio.Provider;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed record EmbeddingData
{
    public string? Object { get; set; }
    
    public List<float>? Embedding { get; set; }
    
    public int? Index { get; set; }
}